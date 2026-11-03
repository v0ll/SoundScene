# Security design

SoundScene runs as the signed-in user and talks to an audio component that is already installed on
the machine. This page describes everything it touches, so the app can be reviewed without reading
every source file.

## No network access

There is no HTTP client, socket, DNS call, telemetry, crash upload, or update check anywhere in
`src`. The app never contacts the internet.

Two local-only mechanisms exist:

- A second launch wakes the running instance through a named mutex and event in the current
  session (`Local\SoundScene.Instance`), not a socket or pipe.
- The setup checklist can open the Microsoft Store page for Dolby Access when you click the button.
  That is a `Process.Start` on a hardcoded `ms-windows-store://` URI.

## Privileges

- The installer is per-user (`PrivilegesRequired=lowest`) and installs to
  `%LOCALAPPDATA%\Programs\SoundScene`.
- No service, driver, scheduled task, or elevation prompt.
- The tray icon uses `Shell_NotifyIcon` directly.

## What is read

| Path | Why |
| --- | --- |
| `C:\Program Files\WindowsApps\DolbyLaboratories.DolbyAccess_*` | the client DLL and the VCLibs dependency |
| `%WINDIR%\System32\DriverStore\FileRepository\dax3_*` | band centre frequencies |
| `HKLM\SYSTEM\CurrentControlSet\Services\DolbyDAXAPI` | diagnostics only, opened read-only |

## What is written

| Path | Contents |
| --- | --- |
| `%APPDATA%\SoundScene\settings.json` | hotkeys, status, window preferences |
| `%APPDATA%\SoundScene\scenes.json` | saved scenes |
| `%APPDATA%\SoundScene\last-error.txt` | the last unhandled exception, if one occurred |
| `%LOCALAPPDATA%\SoundScene\dax-cache\` | the copied Dolby client and VCLibs |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SoundScene` | only when "Start with Windows" is enabled |

A portable copy keeps everything under its own `data` folder and never writes the values above.

## Why Dolby binaries are copied

Windows will not map an executable image from `WindowsApps` into a process that has no package
identity. The client DLL therefore has to be copied before it can be loaded. This is a Windows
rule, not a bypass.

The copy is verified rather than trusted:

- The Dolby Access package must be Windows-signed: `PackageSignatureKind` must not be `None`, and
  the package must not be in development mode. The files inside an MSIX are not individually
  Authenticode-signed, so the package signature is the only trustworthy statement about them.
  This is why SoundScene checks the package and not the DLL.
- Every cached file must SHA-256 match the file in the installed package. A modified or stale cache
  is replaced from the package before it is handed to the loader.
- A cache directory that is a reparse point (junction or symlink) is refused.
- The loader uses `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32`, so imports
  resolve only from the cache directory and System32 — never the application folder, the working
  directory, or `PATH`.

Nothing is redistributed. The bytes come from the copy already installed and licensed on the
machine, and they are deleted by the uninstaller.

## What this does not protect against

- **Malware already running as you.** It can modify the app, your settings, or inject into any of
  your processes. No per-user application can prevent that.
- **A compromised Dolby Access package.** If Windows installed and verified it, SoundScene treats
  it as trusted.
- **A future DAX build that changes its native ABI.** That is a compatibility failure, not a
  security one.

## Build provenance

Release assets are produced by `.github/workflows/release.yml` from the tagged commit and carry a
GitHub build provenance attestation. See
[verify-download.md](verify-download.md) for the commands.

## Reporting a vulnerability

See [SECURITY.md](../SECURITY.md).
