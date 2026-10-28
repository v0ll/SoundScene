# SoundScene

A LIGHTWEIGHT profile switcher that lets you flip between profiles and settings INSTANTLY for **Dolby Access** on ASUS ROG laptops.

From a tray icon, a global hotkey, or a compact popup - instead of opening the Dolby app and clicking through the UI every time, while maintaining around 80MB~ RAM usage.

---

## Why

The ASUS ROG Zephyrus G14 (GA403UV) ships with Dolby Access, which sounds excellent but lacks the quality of life experience. Switching from "Movie" to "Game" means opening the app, waiting for it to load, and
navigating. There is nothing that makes changing profiles easier.

SoundScene aims to be the G-Helper of Dolby audio: small, fast, keyboard-driven, and honest.

## Features

- **Instant profile switching** - main profiles (Movie / Music / Game / Voice / Personalize / Dynamic).
- **Scenes** - save the current profile and custom EQ as a named scene, recall it with one click.
  Stored as plain JSON in your own user profile; Dolby Access has no equivalent.
- **Macro hotkeys** - bind a key combo to "toggle between profile A and profile B". The default is
  `Ctrl+Alt+M` for Movie <-> Game.
- **Tray-first UI** - compact popup for quick switching and scene recall. Number keys 1-6 pick a
  profile, Escape closes it.
- **One process** - a second launch wakes the running instance instead of racing it for the
  same DAX profile.
- **Start with Windows** - optional HKCU Run entry, toggled from the tray menu.
- **EQ editor** - a 20-band graphical EQ matching Dolby's own band layout, with the
  frequencies taken from the driver's tuning XML rather than guessed. Writes the curve and
  switches to Personalize so the change is audible.
- **Backend abstraction** - a Dolby DAX backend plus an Equalizer APO fallback, so the app degrades
  gracefully instead of dying when the Dolby stack is unavailable. The APO backend is designed
  ([ADR 0003](docs/adr/0003-equalizer-apo-fallback.md)) but not implemented.
- **Diagnostics** - the tray menu can dump the real backend state (HRESULTs, endpoints, the
  copied DLL path) for a bug report.

## INSTALLING

Build the native bridge once, then install the app for the current user - no elevation, nothing
outside your profile:

```powershell
cmake --build native\build --config Release
powershell -ExecutionPolicy Bypass -File tools\install.ps1
```

That publishes to `%LOCALAPPDATA%\Programs\SoundScene` and creates a Start Menu shortcut, so the app
can be launched (and pinned to the taskbar) like any other program. `-Desktop` adds a desktop
shortcut too. The published build is framework-dependent: it needs the **.NET 10 Desktop Runtime**,
which the SDK already includes.

Launching it by hand opens the popup. The "Start with Windows" entry runs it with `--background`, so
a login only puts the icon in the tray.

For development, run it from source:

```powershell
dotnet run --project src\SoundScene.App -c Release
```

The app lives in the tray. Left-click the icon for the popup, right-click for the graphic
equalizer, hotkey settings, and exit. Scenes and settings are stored under
`%APPDATA%\SoundScene\`.

A scene records the active profile, and the EQ curve only when it was captured on **Personalize** -
Dolby keeps equalizer levels per profile and only exposes them on that one, so capturing zeros from
Movie would be a lie.

## Architecture

```
SoundScene.App  (WPF / .NET 10)
      |
SoundScene.Core  (profiles, scenes, hotkeys, persistence)
      |
IAudioBackend
      +-- DolbyDaxBackend      -> native C++/WinRT bridge -> DAX.CDolbyDAX
      +-- EqualizerApoBackend  -> Equalizer APO config files
      +-- MockBackend          -> in-memory, for tests and UI work
```

The native bridge is a thin C++/WinRT layer exposing a small C ABI, because the DAX WinRT component
cannot be consumed directly from .NET without a projection we don't control.

## REQUIREMENTS

- Windows 11
- .NET 10 SDK
- Visual Studio 2022 Build Tools with **Desktop development with C++** (for the native bridge)
- Dolby Access installed, with the DAX driver present

## BUILDING

```powershell
dotnet build SoundScene.slnx
```

The native bridge is built separately with CMake once the spike lands:

```powershell
cmake -S native -B native/build -G "Visual Studio 17 2022" -A x64
cmake --build native/build --config Release
```

## TESTING

```powershell
dotnet test SoundScene.slnx -c Release
```

## LEGAL

SoundScene is an independent, unofficial project. It is **not affiliated with, endorsed by, or
supported by Dolby Laboratories or ASUSTeK Computer Inc.** "Dolby", "Dolby Atmos", and "Dolby
Access" are trademarks of Dolby Laboratories.

This project does not include, bundle, decompile, or redistribute any Dolby binaries. It only calls
APIs already installed and licensed on the local machine, in the same way any audio application
does. No copy protection, licensing check, or DRM mechanism is bypassed. If the vendor's
components refuse third-party callers, that is respected - the Equalizer APO backend exists
precisely so the app remains useful in that case.

Use at your own risk. Modifying audio driver state can, in principle, leave a machine with no sound
until the driver is reset. Back up `C:\ProgramData\Dolby\DAX3\Runtime.xml` before experimenting.

The trademark and third-party notice is kept separately in [`NOTICE.md`](NOTICE.md).

## License

MIT - see [LICENSE.md](LICENSE.md).
