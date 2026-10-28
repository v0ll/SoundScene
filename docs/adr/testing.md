# Testing strategy

## The core constraint

The interesting part of this app talks to a vendor component that cannot run in CI. So the test
suite is organised around **what can be tested without Dolby**, and the DAX layer is kept as thin
as physically possible so that almost nothing is left untested.

## Layers

| Layer | Test type | Runs in CI |
| --- | --- | --- |
| `SoundScene.Core` (scenes, hotkey parsing, profile model, persistence) | unit | yes |
| `IAudioBackend` contract | unit, via `MockBackend` | yes |
| `EqualizerApoBackend` (config file generation) | unit, against golden files | yes |
| `DolbyDaxBackend` | integration, manual only | no |
| Native bridge (C++/WinRT) | smoke console, manual only | no |

## Rules

1. **Core has no `using System.Windows` and no P/Invoke.** If it needs either, it belongs elsewhere.
   This keeps Core testable on any machine and makes the WPF layer replaceable.
2. **Every `IAudioBackend` implementation passes the same contract test suite.** Write the contract
   test once, run it against `MockBackend` in CI and against `DolbyDaxBackend` manually on hardware.
3. **Scenes are serialised with a version field.** A test must assert that a v1 scene file still
   loads after the model changes, or the format is not actually stable.
4. **No test may touch `C:\ProgramData\Dolby`.** Integration tests read that path only, and only
   when explicitly opted in via an environment variable.
5. **Hotkey parsing is pure.** `"Ctrl+Alt+M"` -> a key/modifier struct. No Win32 calls in the parser.

## Running

```powershell
dotnet test SoundScene.slnx
```

## Manual DAX checklist

Run these by hand on the G14 after any change to the native bridge, in this order:

1. Read `ActiveProfile` without modifying anything. Does it match Dolby Access?
2. Read all 20 `GEQLevels`. Do they match what the Dolby UI shows?
3. Set one GEQ band to a new value. Confirm the change appears in Dolby Access.
4. Restore the original band value.
5. Switch `ActiveProfile`. Confirm the audible change.
6. Restart Dolby Access. Is the profile still what we set?
7. Reboot. Is it still what we set?

Step 7 is the one that decides whether scenes need an explicit "reapply on startup" mechanism.

## What "done" looks like for the spike

- [ ] Activation succeeds from a non-packaged console process.
- [ ] Profile read matches the Dolby UI.
- [ ] Profile write is visible in the Dolby UI.
- [ ] One GEQ band round-trips (read, write, read back).
- [ ] `Runtime.xml` is backed up and restored after each experiment.
