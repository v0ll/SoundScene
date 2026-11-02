# 0004 - Stay on WPF and cut idle memory instead of switching stacks

## Status

Accepted

## Context

On a cold start the app held roughly 130 MB of working set (about 55 MB of
private commit), and after it had been used - opening the popup and the EQ
editor - Windows reported around 120 MB. For a tray utility that mostly sits
idle, that is the wrong shape: users compare it with G-Helper and Twinkle Tray,
not with an IDE.

The obvious escape hatch is a different UI stack. The realistic options were:

- **WinUI 3 / Windows App SDK.** Reported memory is comparable or slightly lower
  for some apps, but it adds the Windows App SDK runtime, its own deployment and
  startup story, and a full rewrite of every window, style and binding. The
  published comparisons disagree on the memory win, so it is not a safe bet.
- **Avalonia.** Similar footprint to WPF once Skia and its renderer are loaded.
  A rewrite buys cross-platform support the app does not need.
- **Native Win32 / C++.** A tray app in plain Win32 would idle in single-digit
  megabytes, but the EQ editor, scene list, hotkey editor and accessibility
  work would all be rewritten by hand. The project would lose XAML, MVVM and
  the existing UI-automation test for a saving the user only sees while the
  app is idle.

Trimming and Native AOT are not available for WPF, so "make the current build
smaller" cannot mean linker trimming.

## Decision

Stay on WPF and .NET 10. Do not rewrite the UI. Cut the two costs that are
actually under the app's control:

1. **Remove WinForms from the tray.** `NotifyIcon` pulled in
   `System.Windows.Forms`, its primitives, `WindowsFormsIntegration`, GDI+ and
   `System.Drawing` - roughly 18 MB of mapped modules and a second UI toolkit -
   for one icon and nine menu items. `TrayIconService` now calls
   `Shell_NotifyIcon` directly and builds the context menu with
   `CreatePopupMenu` / `TrackPopupMenuEx`. The icon comes from
   `ExtractIconEx` on the executable, so the hand-drawn bitmap is gone too.
   Explorer restarts are handled through the `TaskbarCreated` broadcast.

2. **Make idle cheap.**
   - The popup is created on first use instead of at startup, so
     `--background` starts with a view model and a tray icon only.
   - `System.GC.ConserveMemory=5` keeps the GC heap from growing for no reason.
   - `IdleMemoryTrimmer` runs a compacting collection and
     `SetProcessWorkingSetSize(-1, -1)` once, 20 seconds after the last window
     closes. It is one-shot per idle period: trimming in a loop would only
     cause page churn.

The Windows SDK projection (`Microsoft.Windows.SDK.NET.dll`) stays. It is
loaded because `DaxPackageCache` uses the official
`Windows.Management.Deployment.PackageManager` to locate Dolby Access.
Replacing that with a hand-written native projection or registry scraping would
save a few megabytes at the cost of a much less reliable package lookup.

## Measurements

Measured on the G14 with the published build, `--background`, by
`Get-Process` and the `Working Set - Private` counter:

| State | Working set | Private commit | Private working set |
| --- | ---: | ---: | ---: |
| Before the change, cold start | 131 MB | 55 MB | - |
| Before the change, after use | 260 MB | 189 MB | - |
| After, 45 s after launch | 12 MB | 50 MB | 3.4 MB |
| After, popup open | 96 MB | 91 MB | 39 MB |
| After, popup closed + 20 s idle | 8.5 MB | 77 MB | 2.7 MB |

Reopening the popup after a trim took 11 ms in the UI-automation probe, so the
fault-in cost is not noticeable.

## Consequences

- The app no longer references `System.Windows.Forms` or `System.Drawing`; the
  published folder is smaller and one UI toolkit lighter.
- The tray menu is native, so it follows the Windows theme and keyboard
  behaviour automatically. The "Start with Windows" check is read when the menu
  opens rather than updated in place.
- A trimmed process looks very small in Task Manager but still commits the
  managed heap. That is expected: the pages are returned, not the address
  space.
- If the idle trimmer is later found to cause stutter on slower machines, the
  delay can be raised or the trim dropped without touching the UI.
