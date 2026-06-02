# WindowSill AI Limits Extension

WindowSill extension that displays local Codex/OpenAI and Claude/Anthropic usage limits in a
compact sill, hover preview, settings page, and detailed popup.

Package contents:

- `AiLimitsSill` implements `ISillActivatedByDefault` and `ISillSingleView`.
- The compact bar uses local Codex and Claude Code probes by default.
- Mock data remains available through settings for visual validation.
- The popup shows windows, resets, status, pacing, costs, and manual refresh.
- Provider probes do not persist tokens or display sensitive payloads.

Useful commands:

```powershell
dotnet build .\WindowSillAiLimits.slnx -c Release
dotnet run --project .\tests\WindowSillAiLimits.Tests\WindowSillAiLimits.Tests.csproj -c Release
.\scripts\validate-package.ps1
```
