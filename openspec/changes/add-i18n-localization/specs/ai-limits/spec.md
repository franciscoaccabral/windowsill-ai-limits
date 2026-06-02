## ADDED Requirements

### Requirement: Localized user-facing extension text

The extension SHALL provide localized user-facing text for English (United States) and Portuguese (Brazil) through WindowSill-compatible `.resw` resources.

#### Scenario: WindowSill loads localized extension resources

- **GIVEN** the extension is built and packaged
- **THEN** the package SHALL include a PRI resource index containing `en-US` and `pt-BR` resources
- **AND** `en-US` SHALL be complete enough to act as the fallback language

#### Scenario: User-facing UI resolves localized text

- **GIVEN** the user opens the compact bar, hover preview, detailed popup, settings, or receives an above-expected usage notification
- **THEN** visible labels, tooltips, accessibility names, status labels, forecast text, settings labels, and notification copy SHALL resolve from localized resources
- **AND** provider names, product names, model IDs, protocol fields, setting keys, logs, and sanitized external provider messages SHALL NOT be translated

#### Scenario: No extension-specific language selector

- **GIVEN** the user changes the language through WindowSill or Windows
- **THEN** the extension SHALL rely on the host localization mechanism
- **AND** it SHALL NOT add a separate language selector setting
