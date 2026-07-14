# Roadmap

## Milestone 1 — Core foundation (done)
- [x] Solution skeleton (Core / Data / App / Tests)
- [x] SQLite database + EF Core model
- [x] Automatic cabinet inventory scan (tables, ROMs, media)
- [x] WPF UI shell: Dashboard, Tables, Settings
- [x] Settings persistence (scan folders)

## Milestone 2 — Make it useful on a real cabinet (done)
- [x] PinUP Popper integration (read `PUPDatabase.db`, match games to inventory)
- [x] PinballX integration (XML database import)
- [x] Table ↔ ROM ↔ media matching via VPX script parsing (`cGameName`)
- [x] Health report v1: missing ROMs, missing media, orphaned files

## Milestone 3 — Content management (done)
- [x] Artwork/media management (preview, rename to convention, assign to table)
- [x] ROM management (dedupe, orphan detection, quarantine)
- [x] Automatic dependency resolution (B2S backglass, PuP-Packs, altcolor, altsound)
- [x] DOF/rumble support detection (DOF config scan)

## Milestone 4 — Lifecycle (done)
- [x] Version tracking of tables (history of added/updated files per scan)
- [x] Update notifications (Virtual Pinball Spreadsheet database comparison)
- [x] Excel export
- [x] Backup/restore

## Milestone 5 — Sync & polish
- [ ] Optional cloud synchronization of the database
- [ ] Installer / winget package
- [ ] First stable release
