# Contributing to VPin Commander

Thanks for your interest! VPin Commander aims to be built by and for the virtual pinball community.

## Getting started

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. `git clone` the repo, then `dotnet build` and `dotnet test` from the root.
3. Run the app with `dotnet run --project src/VPinCommander.App`.

## Ground rules

- **Open an issue first** for anything larger than a small fix, so we can agree on the approach before you invest time.
- **Respect the dependency rule**: `App → Data → Core`. Core has no UI or persistence dependencies; pure logic (matching, health rules, parsing) lives there and gets unit tests.
- **Never destroy user files.** Operations that remove content must move it to the quarantine folder or create a backup — see `RomManager` and `BackupService` for the pattern.
- **Schema changes** go through EF Core migrations (`dotnet ef migrations add <Name> --project src/VPinCommander.Data`).
- **Tests**: new logic comes with tests. CI must be green (`dotnet test`).
- Match the existing code style (`.editorconfig` is authoritative; file-scoped namespaces, `var` where the type is apparent).

## Project layout

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the map and [docs/ROADMAP.md](docs/ROADMAP.md) for what's planned.
