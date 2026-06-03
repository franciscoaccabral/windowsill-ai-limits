param(
    [Alias("Version")]
    [string] $ReleaseVersion,
    [switch] $NoOpen,
    [switch] $NoRestart
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$owner = "franciscoaccabral"
$repo = "windowsill-ai-limits"
$packageId = "WindowSillAiLimits"
$apiRoot = "https://api.github.com/repos/$owner/$repo"
$downloadHost = "github.com"
$headers = @{
    "Accept" = "application/vnd.github+json"
    "User-Agent" = "WindowSillAiLimitsInstaller"
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseVersion) -and
    $ReleaseVersion -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "Release version must use vX.Y.Z or vX.Y.Z-prerelease format. Got: $ReleaseVersion"
}

function Get-GitHubRelease {
    if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
        return Invoke-RestMethod -Uri "$apiRoot/releases/latest" -Headers $headers
    }

    return Invoke-RestMethod -Uri "$apiRoot/releases/tags/$ReleaseVersion" -Headers $headers
}

function Assert-ExpectedDownloadUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Url,
        [Parameter(Mandatory = $true)]
        [string] $TagName
    )

    $uri = [Uri] $Url
    $expectedPrefix = "/$owner/$repo/releases/download/$TagName/"

    if ($uri.Scheme -ne "https" -or
        $uri.Host -ne $downloadHost -or
        -not $uri.AbsolutePath.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
        throw "Unexpected release asset download URL: $Url"
    }
}

function Get-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        $Release,
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $asset = @($Release.assets | Where-Object { $_.name -eq $Name }) | Select-Object -First 1
    if (-not $asset) {
        throw "Release $($Release.tag_name) does not contain expected asset: $Name"
    }

    Assert-ExpectedDownloadUrl -Url $asset.browser_download_url -TagName $Release.tag_name
    return $asset
}

function Get-ExpectedHash {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ChecksumPath
    )

    $checksumText = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
    if ($checksumText -notmatch '^(?<hash>[A-Fa-f0-9]{64})(\s|$)') {
        throw "Checksum file is not a valid SHA256 manifest: $ChecksumPath"
    }

    return $Matches.hash.ToLowerInvariant()
}

function Test-WsextAssociation {
    $association = @(
        Get-ItemProperty -Path 'Registry::HKEY_CLASSES_ROOT\.wsext' -ErrorAction SilentlyContinue
        Get-ItemProperty -Path 'Registry::HKEY_CURRENT_USER\Software\Classes\.wsext' -ErrorAction SilentlyContinue
    ) | Where-Object { $_ }

    return $association.Count -gt 0
}

function Get-WindowSillPackageDirectory {
    $localPackages = Join-Path $env:LOCALAPPDATA "Packages"
    if (-not (Test-Path -LiteralPath $localPackages)) {
        return $null
    }

    Get-ChildItem -LiteralPath $localPackages -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'WindowSill|64360VelerSoftware' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Stop-WindowSill {
    $windowSillProcess = @(Get-Process -Name "WindowSill" -ErrorAction SilentlyContinue)
    if ($windowSillProcess.Count -eq 0) {
        Write-Host "[ok] WindowSill is not running."
        return
    }

    Write-Host "[warn] Closing WindowSill before reinstalling the extension..."
    foreach ($process in $windowSillProcess) {
        if ($process.MainWindowHandle -ne 0) {
            [void] $process.CloseMainWindow()
        }
    }

    try {
        Wait-Process -Id @($windowSillProcess | ForEach-Object { $_.Id }) -Timeout 10 -ErrorAction Stop
        Write-Host "[ok] WindowSill closed."
        return
    }
    catch {
        Write-Host "[warn] WindowSill did not close gracefully; stopping it so plugin files can be replaced."
        Stop-Process -Id @($windowSillProcess | ForEach-Object { $_.Id }) -Force -ErrorAction Stop
        Wait-Process -Id @($windowSillProcess | ForEach-Object { $_.Id }) -Timeout 10 -ErrorAction SilentlyContinue
        Write-Host "[ok] WindowSill stopped."
    }
}

function Start-WindowSill {
    if ($NoRestart) {
        Write-Host "Skipping WindowSill restart because -NoRestart was specified."
        return
    }

    $command = Get-Command "windowsill.exe" -ErrorAction SilentlyContinue
    if ($command) {
        Start-Process -FilePath $command.Source
        Write-Host "[ok] WindowSill started: $($command.Source)"
        return
    }

    $startApp = Get-StartApps | Where-Object { $_.Name -eq "WindowSill" } | Select-Object -First 1
    if ($startApp) {
        Start-Process -FilePath "shell:AppsFolder\$($startApp.AppID)"
        Write-Host "[ok] WindowSill started: $($startApp.AppID)"
        return
    }

    Write-Host "[warn] Could not start WindowSill automatically. Start it from the Start menu to load AI Limits."
}

function Remove-ExistingPluginDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PluginDirectory
    )

    if (-not (Test-Path -LiteralPath $PluginDirectory)) {
        Write-Host "[ok] AI Limits is not currently installed under WindowSill local plugins."
        return
    }

    Write-Host "[ok] Existing AI Limits installation found. Removing: $PluginDirectory"
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Remove-Item -LiteralPath $PluginDirectory -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 6) {
                throw "Could not remove the existing AI Limits plugin folder after closing WindowSill. Last error: $($_.Exception.Message)"
            }

            Write-Host "[warn] Plugin files are still being released; retrying removal ($attempt/5)..."
            Start-Sleep -Seconds 2
        }
    }
}

function Install-LocalWindowSillPlugin {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackagePath
    )

    $packageDirectory = Get-WindowSillPackageDirectory
    if (-not $packageDirectory) {
        throw "WindowSill package data was not found under $env:LOCALAPPDATA\Packages. Install or launch WindowSill once, then open this verified file manually: $PackagePath"
    }

    $pluginsRoot = Join-Path $packageDirectory.FullName "LocalState\Plugins"
    $pluginDirectory = Join-Path $pluginsRoot $packageId

    $resolvedParent = [IO.Path]::GetFullPath($pluginsRoot)
    $resolvedTarget = [IO.Path]::GetFullPath($pluginDirectory)
    if (-not $resolvedTarget.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to install outside the WindowSill plugin directory: $resolvedTarget"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Path $pluginsRoot -Force | Out-Null

    Remove-ExistingPluginDirectory -PluginDirectory $pluginDirectory

    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $pluginDirectory)

    $installedDll = Get-ChildItem -LiteralPath $pluginDirectory -Recurse -Filter "$packageId.dll" -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $installedDll) {
        throw "The package was extracted, but $packageId.dll was not found under $pluginDirectory."
    }

    Write-Host "[ok] Installed directly into WindowSill local plugins: $pluginDirectory"
    Write-Host "Start or restart WindowSill to load AI Limits."
}

try {
    $release = Get-GitHubRelease
}
catch {
    if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
        throw "Could not find a latest GitHub release for $owner/$repo. Has a vX.Y.Z tag been published?"
    }

    throw "Could not find GitHub release $ReleaseVersion for $owner/$repo."
}

if ($release.draft) {
    throw "Release $($release.tag_name) is still a draft."
}

if ($release.prerelease -and [string]::IsNullOrWhiteSpace($ReleaseVersion)) {
    throw "Latest release $($release.tag_name) is marked as prerelease. Re-run with -Version $($release.tag_name) if you want it."
}

if ($release.tag_name -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)$') {
    throw "Release tag does not match vX.Y.Z format: $($release.tag_name)"
}

$packageVersion = $Matches.version
$wsextName = "$packageId.$packageVersion.wsext"
$checksumName = "$wsextName.sha256"
$wsextAsset = Get-ReleaseAsset -Release $release -Name $wsextName
$checksumAsset = Get-ReleaseAsset -Release $release -Name $checksumName

$downloadRoot = Join-Path ([IO.Path]::GetTempPath()) "$packageId-$($release.tag_name)-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $downloadRoot | Out-Null

$wsextPath = Join-Path $downloadRoot $wsextName
$checksumPath = Join-Path $downloadRoot $checksumName

Write-Host "Downloading $wsextName from release $($release.tag_name)..."
Invoke-WebRequest -Uri $wsextAsset.browser_download_url -OutFile $wsextPath -Headers $headers
Invoke-WebRequest -Uri $checksumAsset.browser_download_url -OutFile $checksumPath -Headers $headers

$expectedHash = Get-ExpectedHash -ChecksumPath $checksumPath
$actualHash = (Get-FileHash -LiteralPath $wsextPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ($actualHash -ne $expectedHash) {
    throw "Checksum mismatch for $wsextName. Expected $expectedHash, got $actualHash."
}

Write-Host "[ok] SHA256 verified: $actualHash"
Write-Host "[ok] Downloaded to: $wsextPath"

if ($NoOpen) {
    Write-Host "Skipping installer launch because -NoOpen was specified."
    return
}

$windowSillPackageDirectory = Get-WindowSillPackageDirectory
if ($windowSillPackageDirectory) {
    Stop-WindowSill
    Write-Host "[ok] WindowSill package data found: $($windowSillPackageDirectory.FullName)"
    Install-LocalWindowSillPlugin -PackagePath $wsextPath
    Start-WindowSill
    return
}

if (-not (Test-WsextAssociation)) {
    throw ".wsext file association and WindowSill package data were not found. Install or launch WindowSill once, then run this installer again. Verified file: $wsextPath"
}

Write-Host "Opening $wsextName with WindowSill..."
Start-Process -FilePath $wsextPath
Write-Host "If WindowSill was already running, reload or restart it after installation."
