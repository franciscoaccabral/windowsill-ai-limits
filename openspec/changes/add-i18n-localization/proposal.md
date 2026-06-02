# Change: add i18n localization

## Why

The extension currently mixes English and Portuguese user-facing strings directly in C# and XAML. This makes the UI inconsistent and prevents WindowSill from switching copy through its localization system.

## What changes

- Add complete `en-US` and `pt-BR` resource files using WindowSill's `.resw` localization convention.
- Route user-facing UI, settings, and notification strings through localized resources.
- Keep product names and technical identifiers stable: AI Limits, Codex, Claude Code, OpenAI, Anthropic, 5h, 7d, and tokens.
- Format UI dates and numbers through the current culture where appropriate while keeping USD values displayed as USD.

## Out of scope

- Adding an extension-specific language selector.
- Translating logs, provider protocol payloads, model IDs, setting keys, JSON fields, or sanitized external tool error text.
- Changing provider reads, authentication, cache behavior, alert thresholds, or cost calculations.

## Success

The installed extension can display its visible UI in English or Portuguese through WindowSill/Windows language selection, with `en-US` as a complete fallback and `pt-BR` preserving the current Portuguese wording.
