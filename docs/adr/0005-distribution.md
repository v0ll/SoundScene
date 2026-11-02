# 0005 - Ship an Inno Setup installer, a portable ZIP, and require Dolby Access

## Status

Accepted

## Context

SoundScene was distributed as a PowerShell install script. That is fine for a
developer checkout, but it requires the .NET 10 Desktop Runtime, has no
uninstaller, and leaves the user to discover that Dolby Access is a hard
dependency.

The dependency is worth stating precisely. The ASUS/Realtek Dolby Atmos driver
installs the `DolbyDAXAPI` service (`DAX3API.exe`) and the APOs, but the client
API used by SoundScene - `DAXRPCClient.dll` and `DAX.winmd` - ships only inside
the `DolbyLaboratories.DolbyAccess` package. A driver-only machine therefore
launches the app but cannot change profiles or EQ. Bundling the client is not
an option (no redistribution of Dolby binaries), and reimplementing the RPC
surface would be unsupported reverse engineering.

## Decision

- **Installer:** Inno Setup 6, per-user (`PrivilegesRequired=lowest`), no
  elevation, installed to `%LOCALAPPDATA%\Programs\SoundScene`.
- **Runtime:** self-contained `win-x64` publish. A consumer should not have to
  install the .NET runtime first. The installer is roughly 47 MB compressed.
- **Portable:** a second artifact, a ZIP with a `portable.txt` marker. The app
  stores settings, scenes, and the DAX cache in `data\` beside the executable
  and hides "Start with Windows".
- **Requirements:** a checklist in the installer and a first-run window in the
  app. Missing prerequisites warn and allow "Continue anyway"; they do not
  block installation, because Dolby Access can be installed afterwards.
- **Uninstall:** remove the install folder, shortcuts, HKCU Run entry, settings
  and scenes (unless the user chooses to keep them), and the DAX cache. Leave
  Dolby Access, the driver, and `Runtime.xml` alone unless the user opts into
  the Dolby reset.
- **Signing:** unsigned for now. `tools\package.ps1` generates SHA-256 hashes;
  signing can be added later without changing the build shape.
- **Legal:** the self-contained packages include the .NET runtime's
  `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt`, because the release
  redistributes that runtime.
- **Support:** Windows 10 2004 (19041) or newer, x64.

## Consequences

- The release build no longer depends on whatever runtime the user happens to
  have, at the cost of a larger download and install.
- The app is honest about the driver/app split, and points at the Store instead
  of showing a raw "not installed" error.
- A per-user install keeps the uninstaller in Settings > Apps without touching
  Program Files, HKLM, or ProgramData. The only machine-wide component remains
  Dolby's own driver.
- `tools\install.ps1` stays as the developer path, and `tools\uninstall.ps1`
  mirrors the packaged uninstaller's cleanup.
- The uninstaller asks whether to keep settings and whether to reset Dolby;
  silent uninstalls take the destructive-but-clean defaults (delete settings,
  leave Dolby alone).
