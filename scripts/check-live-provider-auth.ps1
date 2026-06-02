$ErrorActionPreference = "Continue"

Write-Host "Live provider auth preflight"
Write-Host "This script prints sanitized local auth readiness only; it does not print tokens, headers, or credential payloads."
Write-Host ""

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
}

$codex = Get-Command "codex" -ErrorAction SilentlyContinue
if ($codex) {
    Show-Ok "codex command found: $($codex.Source)"
    Show-Warn "Codex auth readiness is validated by the opt-in live smoke test; this preflight does not inspect Codex credential files."
}
else {
    Show-Warn "codex command not found. The Codex live smoke test will not be runnable until codex is on PATH."
}

$claude = Get-Command "claude" -ErrorAction SilentlyContinue
if ($claude) {
    Show-Ok "claude command found: $($claude.Source)"
}
else {
    Show-Warn "claude command not found. The Claude live smoke test will not be runnable until claude is on PATH."
}

$credentialsPath = Join-Path $env:USERPROFILE ".claude\.credentials.json"
if (-not (Test-Path -LiteralPath $credentialsPath)) {
    Show-Warn "Claude credentials file not found; run 'claude auth login' before live Claude validation."
    exit 0
}

try {
    $json = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    $oauth = $json.claudeAiOauth

    if ($null -eq $oauth) {
        Show-Warn "Claude credentials file does not contain claudeAiOauth; run 'claude auth login' before live Claude validation."
        exit 0
    }

    if ($null -eq $oauth.expiresAt) {
        Show-Warn "Claude credentials file has no expiresAt metadata; run 'claude auth login' before live Claude validation."
        exit 0
    }

    $expiresAt = [DateTimeOffset]::FromUnixTimeMilliseconds([int64]$oauth.expiresAt)
    $refreshTokenPresent = -not [string]::IsNullOrWhiteSpace($oauth.refreshToken)
    if ($expiresAt -le [DateTimeOffset]::Now.AddMinutes(5)) {
        if ($refreshTokenPresent) {
            Show-Warn "Claude OAuth access token expires at $($expiresAt.UtcDateTime.ToString('o')), but refresh metadata is present. The extension will attempt a safe Claude Code OAuth refresh before live usage."
        }
        else {
            Show-Warn "Claude OAuth credentials are expired or near expiry at $($expiresAt.UtcDateTime.ToString('o')) and no refresh metadata is present; run 'claude auth login' before live Claude validation."
        }
    }
    else {
        Show-Ok "Claude OAuth credentials expire at $($expiresAt.UtcDateTime.ToString('o'))."
    }

    if ($refreshTokenPresent) {
        Show-Ok "Claude refresh metadata is present. Token values are intentionally not printed."
    }

    if (-not [string]::IsNullOrWhiteSpace($oauth.subscriptionType)) {
        Show-Ok "Claude subscription metadata is present: subscriptionType=$($oauth.subscriptionType)."
    }

    if (-not [string]::IsNullOrWhiteSpace($oauth.rateLimitTier)) {
        Show-Ok "Claude rate limit tier metadata is present: rateLimitTier=$($oauth.rateLimitTier)."
    }
}
catch {
    Show-Fail "Claude credential metadata could not be parsed without exposing payloads: $($_.Exception.Message)"
    exit 1
}
