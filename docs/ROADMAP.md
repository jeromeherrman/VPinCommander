# Roadmap

## Milestone 1 — Core foundation (done)
- [x] Solution skeleton (Core / Data / App / Tests)
- [x] SQLite database + EF Core model
- [x] Automatic cabinet inventory scan (tables, ROMs, media)
- [x] WPF UI shell: Dashboard, Tables, Settings
- [x] Settings persistence (scan folders)

## Milestone 2 — Make it useful on a real cabinet (in progress)
- [x] PinUP Popper integration (read `PUPDatabase.db`, match games to inventory)
- [x] PinballX integration (XML database import)
- [ ] Table ↔ ROM ↔ media matching via VPX script parsing (`cGameName`)
- [ ] Health report v1: missing ROMs, missing media, orphaned files

## Milestone 3 — Content management
- [ ] Artwork/media management (preview, rename to convention, assign to table)
- [ ] ROM management (dedupe, orphan detection)
- [ ] Automatic dependency resolution (B2S backglass, PuP-Packs, music, scripts)
- [ ] DOF/rumble support detection (DOF config scan)

## Milestone 4 — Lifecycle
- [ ] Version tracking of tables/backglasses
- [ ] Update notifications (VPUniverse/VPForums feeds where APIs permit)
- [ ] Excel export
- [ ] Backup/restore

## Milestone 5 — Sync & polish
- [ ] Optional cloud synchronization of the database
- [ ] Installer / winget package
- [ ] First stable release
