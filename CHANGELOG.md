# Changelog

## Unreleased

### Added
- Client/server remote management: enable the remote-control server on a cabinet (Settings → cabinet mode: port + generated API key), then manage any number of cabinets from the new "Remote Cabinets" tab on a desktop — live status and inventory counts, health findings, and remotely triggered scans and front-end imports. Plain HTTP on the LAN protected by the API key.
- Release builds are now self-contained: no .NET runtime installation needed on cabinets.

## 0.3.0 — 2026-07-14

### Added
- Media preview before installing or downloading: the Installer shows a preview pane for the selected file — images display directly, videos/audio get a player, and archives can be browsed so the wheel images, backglass videos, and sounds inside are previewable before anything is installed. The Updates tab shows a large VPS preview with version details for the selected table.

### Changed
- The main window now has two tabs: "My Cabinet" (inventory, health, front-ends, settings) and "Downloads" (new table versions and the content installer).
- The Updates list shows preview thumbnails from the VPS database.
- The Installer watches a configurable downloads folder (Settings; defaults to your Windows Downloads folder) and adds new tables/media automatically as they finish downloading.

## 0.2.0 — 2026-07-14

### Added
- Cabinet Health Report v2: the Health page now also reports outdated tables (VPS comparison), missing PuP-Packs and missing DOF coverage (only when the cabinet uses them), tables without any media, duplicate tables and duplicate media, and tables saved with an old Visual Pinball format. Findings are filterable by category as well as severity.
- PinballY integration: imports games from PinballY's PinballX-compatible XML databases, resolving system names and table paths from `Settings.txt`. Third front-end page alongside PinUP Popper and PinballX.
- One-click Installer page: add downloaded files (or scan the Downloads folder) and each piece — table, backglass, PinMAME ROM, PuP-Pack, DMD colorization, AltSound, media — is recognized automatically and installed to its proper cabinet folder. Existing files are never overwritten, unsafe archive paths are rejected, and downloading itself stays in the browser (community sites are login-gated; ROMs are copyrighted).

## 0.1.1 — 2026-07-14

### Fixed
- Opening the ROMs page crashed the app: the "Missing" checkbox column used WPF's default TwoWay binding against a read-only row property. Now explicitly OneWay.
- A corrupt database no longer prevents startup: the file (and its WAL/SHM sidecars) is quarantined as `.corrupt-<timestamp>` and a fresh database is created.
- Unhandled errors now write a crash log to `%APPDATA%\VPinCommander\logs` and show an error dialog instead of silently terminating the app.

## 0.1.0 — 2026-07-14

First release. Everything below landed since the project started.

### Inventory
- Automatic cabinet scan: VPX/FP tables, PinMAME ROMs, media (wheel, backglass, playfield, DMD, topper, audio, video) with folder-heuristic categorization
- VPX metadata parsing (OLE compound file): script-declared ROM (`cGameName`), author, version
- Dependency detection per table: directb2s backglass, PuP-Pack, altcolor, altsound, DOF config coverage
- Version tracking: history of added/updated table files across scans

### Front-ends
- PinUP Popper import (`PUPDatabase.db`, read-only, schema-tolerant)
- PinballX import (per-system XML databases + `PinballX.ini` table path resolution)
- Matching of front-end games to table files (path → filename → name stem)

### Content management
- Media page: category filters, image preview, assign-to-table rename
- ROMs page: reference counts, duplicate/unreferenced filters, quarantine (never deletes)

### Health & lifecycle
- Health report: missing ROMs, missing table files, duplicates, vanished files, orphans
- Update checks against the Virtual Pinball Spreadsheet database (vpsdb)
- Excel export (six sheets)
- Backup/restore and optional folder-based cloud sync
- SQLite storage with EF Core migrations
