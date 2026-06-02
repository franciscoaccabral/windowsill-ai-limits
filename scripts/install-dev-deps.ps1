$ErrorActionPreference = "Stop"

Write-Host "This script installs developer dependencies for WindowSill AI Limits."
Write-Host "It may require administrator approval depending on your Windows setup."
Write-Host ""

winget install Microsoft.DotNet.SDK.8

Write-Host ""
Write-Host "Restart the terminal after SDK installation, then run:"
Write-Host "  dotnet new install WindowSill.Extension.Template"
Write-Host "  dotnet new list windowsill"

