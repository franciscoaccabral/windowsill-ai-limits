param(
    [switch] $IncludeLiveProviders,
    [switch] $IncludeLiveCodex,
    [switch] $IncludeLiveClaude
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "WindowSillAiLimits.slnx"
$testsProject = Join-Path $repoRoot "tests\WindowSillAiLimits.Tests\WindowSillAiLimits.Tests.csproj"

function Invoke-ValidationStep {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,
        [Parameter(Mandatory = $true)]
        [string] $Command,
        [string[]] $Arguments = @(),
        [int] $Attempts = 1
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        Write-Host ""
        if ($Attempts -gt 1) {
            Write-Host "==> $Name (attempt $attempt/$Attempts)"
        }
        else {
            Write-Host "==> $Name"
        }

        & $Command @Arguments

        if ($LASTEXITCODE -eq 0) {
            return
        }

        if ($attempt -lt $Attempts) {
            Write-Host "[warn] $Name failed with exit code $LASTEXITCODE; retrying once."
            Start-Sleep -Seconds 2
            continue
        }

        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Invoke-ValidationStep "Prerequisite inventory" "powershell" @("-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "check-prereqs.ps1"))
Invoke-ValidationStep "Live provider auth preflight" "powershell" @("-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "check-live-provider-auth.ps1"))
Invoke-ValidationStep "Release build" "dotnet" @("build", $solution, "-c", "Release", "--no-restore")
Invoke-ValidationStep "dotnet test" "dotnet" @("test", $solution, "-c", "Release", "--no-restore")
Invoke-ValidationStep "Local harness" "dotnet" @("run", "--project", $testsProject, "-c", "Release", "--no-restore")
Invoke-ValidationStep "Package inspection" "powershell" @("-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "validate-package.ps1"))
Invoke-ValidationStep "WindowSill local preflight" "powershell" @("-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "check-windowsill-local.ps1"))

if ($IncludeLiveProviders -or $IncludeLiveCodex) {
    Invoke-ValidationStep "Live Codex smoke test" "dotnet" @("run", "--project", $testsProject, "-c", "Release", "--no-restore", "--", "--live-codex") -Attempts 2
}

if ($IncludeLiveProviders -or $IncludeLiveClaude) {
    Invoke-ValidationStep "Live Claude smoke test" "dotnet" @("run", "--project", $testsProject, "-c", "Release", "--no-restore", "--", "--live-claude") -Attempts 2
}

if (-not $IncludeLiveProviders -and -not $IncludeLiveCodex -and -not $IncludeLiveClaude) {
    Write-Host ""
    Write-Host "[skip] Live provider smoke tests. Re-run with -IncludeLiveCodex, -IncludeLiveClaude, or -IncludeLiveProviders to query local authenticated providers."
}

Write-Host ""
Write-Host "[ok] Local validation completed."
