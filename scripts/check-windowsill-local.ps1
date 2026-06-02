$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts"

Write-Host "WindowSill local preflight"
Write-Host "This script reads local install/package evidence only; it does not launch WindowSill."
Write-Host ""

$failed = $false

function Show-Ok {
    param([string] $Message)
    Write-Host "[ok]   $Message"
}

function Show-Warn {
    param([string] $Message)
    Write-Host "[warn] $Message"
}

function Show-Fail {
    param([string] $Message)
    Write-Host "[fail] $Message"
    $script:failed = $true
}

$command = Get-Command "windowsill.exe" -ErrorAction SilentlyContinue
if ($command) {
    Show-Ok "windowsill.exe alias found: $($command.Source)"
}
else {
    Show-Fail "windowsill.exe alias not found in PATH."
}

$packageDirs = @()
$localPackages = Join-Path $env:LOCALAPPDATA "Packages"
if (Test-Path -LiteralPath $localPackages) {
    $packageDirs = @(Get-ChildItem -LiteralPath $localPackages -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'WindowSill|64360VelerSoftware' })
}

if ($packageDirs.Count -gt 0) {
    foreach ($dir in $packageDirs) {
        Show-Ok "WindowSill package data found: $($dir.FullName)"
    }
}
else {
    Show-Warn "No WindowSill package data directory found under $localPackages."
}

$package = Get-ChildItem -Path $artifactsDir -Filter "WindowSillAiLimits.*.wsext" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($package) {
    Show-Ok "Local .wsext artifact found: $($package.FullName)"
}
else {
    Show-Fail "No WindowSillAiLimits .wsext found in $artifactsDir. Run dotnet build -c Release first."
}

if ($package -and $packageDirs.Count -gt 0) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $installedPluginDll = $packageDirs |
        ForEach-Object {
            $pluginsDir = Join-Path $_.FullName "LocalState\Plugins\WindowSillAiLimits"
            if (Test-Path -LiteralPath $pluginsDir) {
                Get-ChildItem -LiteralPath $pluginsDir -Recurse -Filter "WindowSillAiLimits.dll" -File -ErrorAction SilentlyContinue
            }
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($installedPluginDll) {
        $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        $tempDll = Join-Path ([IO.Path]::GetTempPath()) "WindowSillAiLimits-package-$([Guid]::NewGuid().ToString('N')).dll"
        try {
            $packageDllEntry = $zip.Entries |
                Where-Object { $_.FullName -like "lib/*/WindowSillAiLimits.dll" } |
                Select-Object -First 1

            if (-not $packageDllEntry) {
                Show-Fail "Local .wsext does not contain lib/*/WindowSillAiLimits.dll."
            }
            else {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($packageDllEntry, $tempDll, $true)
                $packageHash = (Get-FileHash -LiteralPath $tempDll).Hash
                $installedHash = (Get-FileHash -LiteralPath $installedPluginDll.FullName).Hash

                if ($packageHash -eq $installedHash) {
                    Show-Ok "Installed WindowSillAiLimits DLL matches current .wsext artifact."
                }
                else {
                    Show-Warn "Installed WindowSillAiLimits DLL differs from current .wsext artifact. Reinstall/open the latest .wsext before validating in WindowSill."
                    Show-Warn "Installed: $($installedPluginDll.FullName) ($($installedPluginDll.LastWriteTime))"
                    Show-Warn "Artifact:  $($package.FullName) ($($package.LastWriteTime))"
                }
            }
        }
        finally {
            $zip.Dispose()
            if (Test-Path -LiteralPath $tempDll) {
                Remove-Item -LiteralPath $tempDll -Force
            }
        }
    }
    else {
        Show-Warn "WindowSillAiLimits is not currently installed under WindowSill LocalState\Plugins."
    }
}

$association = @(
    Get-ItemProperty -Path 'Registry::HKEY_CLASSES_ROOT\.wsext' -ErrorAction SilentlyContinue
    Get-ItemProperty -Path 'Registry::HKEY_CURRENT_USER\Software\Classes\.wsext' -ErrorAction SilentlyContinue
) | Where-Object { $_ }

if ($association.Count -gt 0) {
    Show-Ok ".wsext file association exists in registry."
}
else {
    Show-Warn ".wsext file association not found. Loading the extension still needs WindowSill UI/manual validation."
}

if ($failed) {
    exit 1
}
