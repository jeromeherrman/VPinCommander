# Changelog

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
