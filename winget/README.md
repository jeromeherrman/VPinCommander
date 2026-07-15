# winget packaging

The manifests under `manifests/` mirror what gets submitted to
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
`winget validate --manifest winget/manifests/j/JeromeHerrman/VPinCommander/<version>` must pass.

## Submitting a version

First time (or manually):

1. Fork `microsoft/winget-pkgs` and create a branch.
2. Copy `winget/manifests/j/JeromeHerrman/VPinCommander/<version>/` into the fork at
   `manifests/j/JeromeHerrman/VPinCommander/<version>/`.
3. Open a PR titled `New package: JeromeHerrman.VPinCommander version <version>`
   (or `New version: …` for updates). Automated validation runs on the PR;
   a moderator merges it, and the package appears in winget shortly after.

Subsequent versions can be automated with
[wingetcreate](https://github.com/microsoft/winget-create):

```
wingetcreate update JeromeHerrman.VPinCommander `
  --version <X.Y.Z> `
  --urls https://github.com/jeromeherrman/VPinCommander/releases/download/v<X.Y.Z>/VPinCommander-v<X.Y.Z>-win-x64.zip `
  --submit
```

## Notes

- The release asset is a zip containing a portable, self-contained exe, so the
  manifest uses `InstallerType: zip` + `NestedInstallerType: portable`
  (alias: `vpincommander`). winget extracts the whole archive, keeping the
  native DLLs next to the exe.
- `InstallerSha256` must match the exact release asset
  (`Get-FileHash <zip> -Algorithm SHA256`).
- Users install with `winget install JeromeHerrman.VPinCommander`
  and upgrade with `winget upgrade` — alongside the app's own self-updater.
