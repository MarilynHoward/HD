# HD Project (Migrated from Kiro to Cursor)

This workspace was migrated from `D:\Dev\Kiro\HD` to `D:\Dev\Cursor\HD`.

## Migration Notes

- Source project content was copied into this workspace.
- Editor/build artifacts were excluded during migration (`.vs`, `bin`, `obj`, `*.user`).
- Source `.git` metadata was not retained in this workspace.
- A fresh Git repository was initialized in this folder.

## Standards and Steering

Project coding and design standards are now enforced through a Cursor rule:

- `.cursor/rules/wpf-usercontrol-framework-standards.mdc`

That rule was derived from:

- `.kiro/steering/wpf-conventions.md`
- `WpfUserControlFramework/Docs/SteeringDocument.md`

## Validation Snapshot

Latest validation commands run in this migrated workspace:

- `dotnet build WpfUserControlFramework.sln` -> passed
- `dotnet test WpfUserControlFramework.Tests/WpfUserControlFramework.Tests.csproj` -> passed (`1` passed, `0` failed)
