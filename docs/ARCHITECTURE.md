# Architecture

## Projects

```
src/
  VPinCommander.Core/   Domain models + services. No UI or persistence dependencies.
  VPinCommander.Data/   EF Core 8 + SQLite persistence (references Core).
  VPinCommander.App/    WPF desktop app, MVVM via CommunityToolkit.Mvvm (references Core + Data).
tests/
  VPinCommander.Core.Tests/  xUnit tests for Core services.
```

Dependency rule: `App → Data → Core`. Core never references Data or App, so services stay testable and a future CLI/daemon can reuse them.

## Key concepts

- **GameTable** — a playable table file (`.vpx` Visual Pinball X, `.fp` Future Pinball) found on disk.
- **Rom** — a PinMAME ROM archive (`.zip`) in a ROM folder.
- **MediaAsset** — artwork/audio/video (wheel images, backglass, playfield video, DMD, launch audio…), categorized by extension and folder heuristics.
- **ScanRun** — one execution of the inventory scanner, with counts and timing, kept as history.

## Inventory scanning (Milestone 1)

`InventoryScanner` (Core) walks user-configured folders and produces a `ScanResult` of tables, ROMs, and media. `InventoryService` (Data) upserts results into SQLite keyed by absolute file path, marking records missing from a scan as `IsMissing` instead of deleting them (so health reports can flag removed files later).

Matching between tables ↔ ROMs ↔ media is filename-stem based in M1. Later milestones will parse the table script from the VPX OLE compound file to read the real `cGameName` ROM reference.

## Persistence

- SQLite database at `%APPDATA%\VPinCommander\vpincommander.db`.
- EF Core with `EnsureCreated` during M1; will switch to migrations before the first public release.
- App settings (folder paths, preferences) as JSON at `%APPDATA%\VPinCommander\settings.json` via `SettingsService`.

## UI

WPF, MVVM. `MainViewModel` owns navigation; each page is a ViewModel + DataTemplate-mapped View (Dashboard, Tables, Settings). Dependency injection via `Microsoft.Extensions.Hosting` generic host, composed in `App.xaml.cs`.

## Future modules (planned seams)

- `Integrations/` folder in Core defines interfaces (`IFrontEndIntegration`) that PinUP Popper (SQLite `PUPDatabase.db`) and PinballX (XML databases + ini) adapters will implement.
- Dependency resolution, health reports, Excel export, backup/restore, and cloud sync each become services over the same Core model.
