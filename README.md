# VPin Commander

**The most complete open source manager for virtual pinball cabinets.**

VPin Commander scans your cabinet, builds a full inventory of your tables, ROMs, and media, and helps you keep everything healthy, backed up, and up to date. It integrates with the front-ends you already use.

> ⚠️ Early development. The core foundation (inventory scanning, SQLite database, UI shell) is being built first — see the [roadmap](docs/ROADMAP.md).

## Planned features

| Area | Features |
|---|---|
| Inventory | Automatic cabinet inventory, version tracking, update notifications |
| Content | Artwork management, media management, ROM management, automatic dependency resolution |
| Health | Health reports, DOF/rumble support detection |
| Integration | PinUP Popper integration, PinballX integration |
| Data | SQLite database, Excel export, backup/restore, optional cloud synchronization |
| Remote | Client/server cabinet management, content push, HTTPS pairing, Android companion app |

## Install

Windows 10/11. No runtime to install — the builds are self-contained.

- **Installer (recommended):** download `VPinCommander-Setup-<version>.exe` from the [latest release](https://github.com/jeromeherrman/VPinCommander/releases/latest) and run it. It installs per-user (no admin prompt), adds a Start Menu entry, and keeps itself up to date from within the app (Settings → Application updates).
- **Portable / cabinet:** download `VPinCommander-<version>-win-x64.zip`, extract anywhere, and run `VPinCommander.exe`.
- **Android companion:** `VPinCommander-<version>-android.apk`.

## Documentation

- [User guide](docs/USER_GUIDE.md) — everyday use, from first run to remote management. The same material is on the in-app **Help** page.

## Building from source

```powershell
git clone https://github.com/jeromeherrman/VPinCommander.git
cd VPinCommander
dotnet build
dotnet test
dotnet run --project src/VPinCommander.App
```

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). In short:

- `VPinCommander.Core` — domain models and services (no UI, no database dependencies)
- `VPinCommander.Data` — SQLite persistence via EF Core
- `VPinCommander.App` — WPF desktop app (MVVM with CommunityToolkit.Mvvm)

## Contributing

Contributions are welcome — this project aims to be built by and for the virtual pinball community. Open an issue to discuss a feature before starting large work.

## License

[MIT](LICENSE)
