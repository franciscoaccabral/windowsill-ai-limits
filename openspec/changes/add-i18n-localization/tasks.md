# Tasks

- [x] 1. Add OpenSpec requirement for extension localization.
- [x] 2. Add failing tests for resource files, localized helper, XAML UIDs, and package resource inclusion.
- [x] 3. Replace legacy `Misc.resw` with complete `Resources.resw` files for `en-US` and `pt-BR`.
- [x] 4. Add a centralized localized text helper.
- [x] 5. Migrate visible UI, settings, and notification text to localized resources.
- [x] 6. Update package validation to require localized resource coverage.
- [x] 7. Run `dotnet build .\WindowSillAiLimits.slnx -c Release`.
- [x] 8. Run `dotnet test .\WindowSillAiLimits.slnx -c Release`.
- [x] 9. Run `dotnet run --project .\tests\WindowSillAiLimits.Tests\WindowSillAiLimits.Tests.csproj -c Release`.
- [x] 10. Run `scripts\validate-package.ps1`.
- [ ] 11. Manually validate `en-US` and `pt-BR` in the installed WindowSill host when available.
