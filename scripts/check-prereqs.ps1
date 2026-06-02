$ErrorActionPreference = "Continue"

function Show-Ok {
    param([string] $Message)
    Write-Host "[ok]      $Message"
}

function Show-Warn {
    param([string] $Message)
    Write-Host "[warn]    $Message"
}

function Show-CommandVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,
        [string] $VersionArg = "--version"
    )

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Write-Host "[missing] $Name"
        return
    }

    Write-Host "[found]   $Name -> $($cmd.Source)"
    try {
        & $Name $VersionArg
    }
    catch {
        Write-Host "[warn]    could not get version for ${Name}: $($_.Exception.Message)"
    }
}

Show-CommandVersion dotnet "--info"
Show-CommandVersion codex "--version"
Show-CommandVersion claude "--version"
Show-CommandVersion node "--version"
Show-CommandVersion npm "--version"
Show-CommandVersion git "--version"

Write-Host ""
Write-Host ".NET SDKs:"
dotnet --list-sdks

Write-Host ""
Write-Host "WindowSill templates:"
$templateOutput = dotnet new list windowsill 2>&1
if ($LASTEXITCODE -eq 0) {
    Show-Ok "WindowSill extension template is available."
    $templateOutput
}
else {
    Show-Warn "WindowSill extension template is not currently installed for dotnet new."
    Show-Warn "The existing project can still build and package; reinstall the template only if new scaffolds are needed."
}
