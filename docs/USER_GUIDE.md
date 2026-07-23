# VPin Commander — User Guide

VPin Commander manages the contents of a virtual pinball cabinet: tables, ROMs,
media, and the front-end that launches them. This guide walks through everyday
use. There's also a **Help** page inside the app (in the sidebar) with the same
material in short form.

## Contents

- [Installing](#installing)
- [First run](#first-run)
- [The Dashboard](#the-dashboard)
- [Tables, ROMs and Media](#tables-roms-and-media)
- [Health report](#health-report)
- [Downloading and installing content](#downloading-and-installing-content)
- [Front-ends: PinUP Popper, PinballX, PinballY](#front-ends)
- [Managing multiple cabinets](#managing-multiple-cabinets)
- [Backup, cloud sync and export](#backup-cloud-sync-and-export)
- [Keeping the app updated](#keeping-the-app-updated)
- [Where your data lives](#where-your-data-lives)
- [Troubleshooting](#troubleshooting)

## Installing

Windows 10/11. The builds are self-contained — there's no separate runtime to
install.

- **Installer (recommended):** download `VPinCommander-Setup-<version>.exe` from
  the [latest release](https://github.com/jeromeherrman/VPinCommander/releases/latest)
  and run it. It installs per-user (no admin prompt), adds a Start Menu entry,
  and keeps itself up to date from inside the app.
- **Portable / cabinet:** download `VPinCommander-<version>-win-x64.zip`, extract
  it anywhere, and run `VPinCommander.exe`.
- **Android companion:** install `VPinCommander-<version>-android.apk` to manage
  cabinets from your phone.

## First run

1. Open **Settings** (sidebar).
2. Add your cabinet folders using the **Browse** / **Add folder** buttons:
   - **Table folders** — where your `.vpx` / `.fp` files live.
   - **ROM folders** — your PinMAME `roms` folder(s) of `.zip` files.
   - **Media folders** — wheel images, backglass, playfield videos, etc.
3. If you use a front-end, set its install folder (**PinUP Popper**, **PinballX**,
   or **PinballY**). These are usually auto-detected.
4. Optionally set the **Downloads folder to monitor** (defaults to your Windows
   Downloads folder) and the **DOF config folder**.
5. Click **Save settings**.
6. Go to the **Dashboard** and click **Scan cabinet**.

The scan reads your files and builds an inventory. Re-run it whenever you add or
remove content — every other page reflects the most recent scan.

## The Dashboard

Shows counts of tables, ROMs, media, and front-end games, plus any missing
files. From here you **Scan cabinet** and **Export to Excel**.

## Tables, ROMs and Media

**Tables** lists every table found, with its ROM, version, author, and which
extras it has — backglass (B2S), PuP-Pack, DOF, AltColor, AltSound. The ROM name
comes from reading each table's own script, so it reflects what the table
actually needs, not just its file name.

**ROMs** shows how many tables reference each ROM, and flags duplicates and
unreferenced ROMs. **Quarantine** moves a ROM into a safe folder
(`%APPDATA%\VPinCommander\Quarantine`) instead of deleting it — nothing is ever
destroyed.

**Media** lets you preview images and **assign a media file to a table**. Assigning
renames the file to match the table so front-ends pick it up automatically.
Filter by category or show only unassigned files.

## Health report

The **Health** page checks your cabinet and lists issues by severity and category:

- **Errors** — a table's ROM is missing, or a front-end lists a game with no
  table file.
- **Warnings** — outdated tables (versus the community database) and duplicate
  files.
- **Info** — tables without a backglass, PuP-Pack, DOF coverage, or media; unused
  ROMs and media; and tables saved in an old Visual Pinball format.

PuP-Pack and DOF findings only appear if your cabinet uses them at all, so you're
not flooded with notices that don't apply. Filter by severity and category, and
re-run the check any time.

## Downloading and installing content

The **Downloads** tab has two parts.

**New table versions**
- **Check for updates** compares your tables against the community Virtual
  Pinball Spreadsheet (vpsdb) and lists newer versions.
- **Browse all tables** shows the entire catalog of installable tables — use the
  search box to find something new.
- Select any table to see its preview image and version, then **Open download
  page** to get it from the community site in your browser.

**Install downloaded content**
- After you download a file, VPin Commander detects it automatically (it watches
  your Downloads folder) — or click **Add files** to pick some manually.
- It recognizes tables, backglasses, ROMs, PuP-Packs, DMD colorizations,
  AltSound, and media, and installs each into the correct folder.
- Existing files are never overwritten, and you can preview the media inside an
  archive before installing.

Downloading itself happens in your browser: the community sites require a login,
and ROMs are copyrighted, so VPin Commander organizes what you download rather
than fetching it for you.

## Front-ends

VPin Commander reads your front-end's game list and matches it to your scanned
tables. Open the relevant page — **PinUP Popper**, **PinballX**, or **PinballY** —
and click **Import**. Games are matched to your table files; any game with no
matching file is highlighted so you can spot gaps. Set the front-end's install
folder in Settings if it isn't detected automatically.

## Managing multiple cabinets

You can manage several cabinets from one computer, or from a phone browser.

**On each cabinet (Settings → remote-control server):**
1. Enable the server and note the **port** (default 5588).
2. **Generate** an API key.
3. For cabinets reachable outside your home network, turn on **HTTPS** — clients
   pair automatically by pinning the certificate on first connect.
4. Save. If clients can't connect, allow VPin Commander through Windows Firewall.

**From your desktop (Remote Cabinets tab):**
Add each cabinet by name, address (`http://cabinet-pc:5588`), and API key. You can
then see its status and health, run scans and imports remotely, and **push
downloaded content** to it.

**From any browser:**
Open `http://<cabinet>:5588/` on a phone, tablet, or laptop, enter the API key,
and manage the cabinet with nothing to install.

## Backup, cloud sync and export

- **Backup / restore** (Settings) — save the database and settings to a zip, and
  restore them later. A restore keeps a copy of the current database first.
- **Cloud sync** (Settings) — point the app at a folder your cloud client already
  syncs (OneDrive, Dropbox, Google Drive…). **Push** writes your data there;
  **Pull** restores it on another machine.
- **Excel export** (Dashboard) — export the whole inventory to a spreadsheet with
  sheets for tables, ROMs, media, front-end games, health, and version history.

## Keeping the app updated

VPin Commander updates itself. When a newer version exists, the window title says
so at startup. Go to **Settings → Application updates**, click **Check for
updates**, then **Download & install**. The app closes, updates, and reopens on
its own. Installing with the Setup installer (rather than the portable zip) gives
the smoothest updates.

## Where your data lives

Your inventory database, settings, logs, quarantined ROMs, and backups live in:

```
%APPDATA%\VPinCommander
```

Your actual pinball content (tables, ROMs, media) stays in the folders you
configured — VPin Commander reads and organizes it there and never moves it
except when you explicitly install, assign, or quarantine.

## Troubleshooting

- **A scan finds nothing** — check the folders in Settings point at the right
  places, and that they contain `.vpx`/`.fp`, ROM `.zip`, or media files.
- **Health shows missing ROMs I have** — make sure your ROM folder is set in
  Settings, then re-scan. ROM names must match what the table script requests.
- **Update check says "unavailable"** — usually a temporary network issue; try
  again, or download the release manually from GitHub.
- **A remote cabinet won't connect** — confirm the server is enabled on the
  cabinet, the port and API key match, and VPin Commander is allowed through the
  cabinet's Windows Firewall.
- **Something crashed** — a log is written to `%APPDATA%\VPinCommander\logs`.
  Attach it when [reporting an issue](https://github.com/jeromeherrman/VPinCommander/issues).
