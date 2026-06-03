param(
    [Alias("Version")]
    [string] $ReleaseVersion,
    [switch] $NoOpen
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

$association = @(
    Get-ItemProperty -Path 'Registry::HKEY_CLASSES_ROOT\.wsext' -ErrorAction SilentlyContinue
    Get-ItemProperty -Path 'Registry::HKEY_CURRENT_USER\Software\Classes\.wsext' -ErrorAction SilentlyContinue
) | Where-Object { $_ }

if ($association.Count -eq 0) {
    throw ".wsext file association was not found. Install or launch WindowSill once, then open this verified file manually: $wsextPath"
}

Write-Host "Opening $wsextName with WindowSill..."
Start-Process -FilePath $wsextPath
Write-Host "If WindowSill was already running, reload or restart it after installation."
