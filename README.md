# SoundScene

A lightweight profile switcher and scene controller for **Dolby Access** on ASUS ROG laptops.

SoundScene lets you flip between Dolby audio profiles and equalizer settings instantly - from a
tray icon, a global hotkey, or a compact popup - instead of opening Dolby Access and clicking
through its UI every time.

> **Status: working prototype.**
> Profile switching, A/B toggle hotkeys, the scene library, and the 20-band EQ
> editor all work against real Dolby Access on the G14. See
> [`docs/adr/0002-dax-backend-viability.md`](docs/adr/0002-dax-backend-viability.md)
> for how the DAX layer works and what it depends on.

---

## Why

The ASUS ROG Zephyrus G14 (GA403UV) ships with Dolby Access, which sounds excellent but is slow to
operate. Switching from "Movie" to "Game" means opening a UWP app, waiting for it to load, and
navigating a profile grid. There is no global hotkey, no CLI, and no scripting surface.

SoundScene aims to be the G-Helper of Dolby audio: small, fast, keyboard-driven, and honest about
what it can and cannot do.

## Features

- **Instant profile switching** - main profiles (Movie / Music / Game / Voice / Personalize / Dynamic).
- **Scenes** - save the current profile and custom EQ as a named scene, recall it with one click.
  Stored as plain JSON in your own user profile; Dolby Access has no equivalent.
- **Macro hotkeys** - bind a key combo to "toggle between profile A and profile B". The default is
  `Ctrl+Alt+M` for Movie <-> Game.
- **Tray-first UI** - compact popup for quick switching and scene recall.
- **EQ editor** - a 20-band graphical EQ matching Dolby's own band layout, with the
  frequencies taken from the driver's tuning XML rather than guessed. Writes the curve and
  switches to Personalize so the change is audible.
- **Backend abstraction** - a Dolby DAX backend plus an Equalizer APO fallback, so the app degrades
  gracefully instead of dying when the Dolby stack is unavailable. The APO backend is planned.

## Using it

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

## Requirements

- Windows 11
- .NET 10 SDK
- Visual Studio 2022 Build Tools with **Desktop development with C++** (for the native bridge)
- Dolby Access installed, with the DAX driver present

## Building

```powershell
dotnet build SoundScene.slnx
```

The native bridge is built separately with CMake once the spike lands:

```powershell
cmake -S native -B native/build -G "Visual Studio 17 2022" -A x64
cmake --build native/build --config Release
```

## Legal

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

## License

MIT - see [LICENSE.md](LICENSE.md).
