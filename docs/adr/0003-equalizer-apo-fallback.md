# ADR 0003 - Equalizer APO as the fallback backend

- Status: proposed
- Date: 2026-10-28
- Supersedes: none

## Context

The Dolby backend depends on undocumented COM surface that a Dolby Access
update can change or close (ADR 0002). It also only exists on machines with
Dolby Access and the DAX driver - which is this G14, but not every machine a
user might want to run SoundScene on. When DAX is unavailable the app currently
has nothing to fall back to.

Equalizer APO is the standard open-source system EQ for Windows. It is a
per-endpoint APO driven by text config files, so a backend can write a curve
without touching any vendor component.

## Decision

Keep `IAudioBackend` as the seam and add `EqualizerApoBackend` behind it. Do
not implement it until it can be tested on a machine with Equalizer APO
installed; this ADR fixes the design so the app's abstractions do not drift.

## Design

**Detection.** Equalizer APO installs to `%ProgramFiles%\EqualizerAPO` and reads
`config\config.txt`. `IsAvailable` is "that file exists". No registry probing
and no service calls.

**Config file.** `config.txt` is shared by every endpoint and lives under
`Program Files`, so writing it needs elevation. The app therefore owns a
separate file, `config\SoundScene.txt`, and the one-time setup is a single
`Include: SoundScene.txt` line in `config.txt`. The app can add that line when
it is running elevated, or tell the user to paste it. Everything after setup is
a normal file write.

**Curve format.** Equalizer APO's `GraphicEQ` filter is a list of
frequency/gain pairs, which maps exactly onto what DAX exposes:

```
Preamp: -6 dB
GraphicEQ: 47 3.0; 141 2.1; 234 0.0; ... 19688 -2.5
```

`GraphicEqualizer.Frequencies` is already the band table, and
`GraphicEqualizer.ToDecibels` already converts the 1/16 dB units. The same
editor and the same scene format work unchanged. `Preamp` is set to the
negative of the largest boost so a boosted curve cannot clip.

**Profiles.** APO has no profile concept, so a profile becomes a named file:
`SoundScene.txt` includes `profiles\<name>.txt`, and switching a profile
rewrites that one include line. That is the whole profile model - no DAX
equivalents for Movie/Music/Game processing, because APO has none. The profile
names stay, but they select curves rather than Dolby's processing.

**Reload.** Equalizer APO reads its config when an audio stream is created, so a
write applies to the next stream, not the current one. The backend must say so
in its status line instead of pretending the change is instant. Whether the
installed version watches the file is version-dependent and must be measured on
the target machine before this is written.

**State.** `GetStateAsync` reports the profile from the active include and the
levels parsed back out of the file. Parsing is the honest source of truth: it
proves the write landed.

## Consequences

- The app degrades instead of failing: no Dolby, but EQ, scenes, hotkeys, and
  the popup still work.
- The EQ editor needs no changes. `GraphicEqualizer` and the scene format are
  shared.
- Users lose Dolby-only processing (surround virtualiser, dialog enhancer, bass
  enhancement, intelligent EQ). Scenes captured on Dolby cannot be replayed on
  APO beyond their profile and EQ; the backend must say which parts it dropped
  rather than silently applying half a scene.
- Backend selection moves into the app: try Dolby, then APO, then report that
  neither is available. This belongs in one factory, not in `App.xaml.cs`.
- Elevation is a real UX cost. The Include line is the only privileged step and
  must stay a one-time setup.
