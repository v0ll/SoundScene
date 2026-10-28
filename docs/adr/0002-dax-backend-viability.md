# ADR 0002 - Dolby DAX backend is viable

- Status: accepted
- Date: 2026-10-28
- Supersedes: none

## Context

The whole product depends on one question: can a normal desktop process read and
write Dolby DAX state? Dolby Access is an MSIX package, `DAX.CDolbyDAX` is an
in-process WinRT component, and the DAX service is capability-gated. There was a
real chance this was impossible.

## What we tried

All from an unpackaged x64 desktop process, .NET 10 host, native bridge in C++.

| # | Approach | Result |
| --- | --- | --- |
| A | `RoGetActivationFactory(L"DAX.CDolbyDAX")` | `0x80040154 REGDB_E_CLASSNOTREG` |
| B1 | `LoadLibraryExW` on `...\WindowsApps\...\DAXRPCClient.dll` | `0x80070005 E_ACCESSDENIED` |
| B2 | same, with the AppContainer CRT directory added | `0x80070005 E_ACCESSDENIED` |
| B3 | same path, `LOAD_LIBRARY_AS_DATAFILE` | OK (mapped, not executable) |
| B4 | same DLL copied to `%TEMP%` | **OK** |
| C | App Service | not attempted, capability-gated |

### Why B1/B2 fail but B4 works

The DLL's ACL grants `BUILTIN\Users` read + execute, and the bytes are readable.
`LOAD_LIBRARY_AS_DATAFILE` succeeds. Only *executable* mapping fails. So this is
a **path-based loader policy**: Windows will not map executable images out of
`C:\Program Files\WindowsApps` into a process that has no package identity. It is
not an ACL problem, not a CRT problem, and not Smart App Control (which is off on
this machine).

### The dependency trap

`DAXRPCClient.dll` imports `vccorlib140_app.dll`, `MSVCP140_APP.dll`,
`VCRUNTIME140_APP.dll` - the AppContainer variants, which do **not** exist in
`System32`. They ship in the `Microsoft.VCLibs.140.00` framework package. Loading
the client DLL without that directory on the search path fails with
`0x8007007E ERROR_MOD_NOT_FOUND`. `AddDllDirectory` on the VCLibs package folder
fixes it.

## Decision

**Adopt `DolbyDaxBackend` as the primary backend.**

Mechanism, implemented in `native/SoundScene.DaxBridge`:

1. Resolve the Dolby Access package folder via `PackageManager` (the
   `WindowsApps` directory itself cannot be enumerated).
2. Copy `DAXRPCClient.dll` plus the `*_app.dll` runtime files into
   `%LOCALAPPDATA%\SoundScene\dax-cache\`.
3. `AddDllDirectory(cache)` + `LoadLibraryExW(cache\DAXRPCClient.dll)`.
4. `DllGetActivationFactory("DAX.CDolbyDAX")` -> `ActivateInstance` ->
   `QueryInterface(DAX.IDolbyDAX)`.
5. Call `OpenRPCEndpoint()`.

No C++/WinRT projection is used. The `IDolbyDAX` vtable is hand-written from the
IDL that `winmdidl` produced from `DAX.winmd` (see `native/idl/DAX.idl`). This
keeps the bridge dependency-free: no NuGet, no generated headers, plain MSVC.

## Verified behaviour

Reads, all `S_OK`:

- `DolbyEnable` = true
- `ActiveProfile` = 0 (Movie)
- `ActiveSubProfile` = 0
- `IEQ` = 3
- `GetAPIStatus` = 1
- `GetGEQMaximumRange` = 192
- `GEQLevels` = 20 bands
- `EndpointCount` = 2, both `Headphones (QCY MeloBuds Pro)`
- `DaxEndpoints` = `{092DF914-4E1D-4C69-8105-2E4850C58864}`

Writes:

- `ActiveProfile` 0 -> 1 -> 0 round-trips, confirmed by read-back.
- `GEQLevels[0]` 0 -> 48 -> 0 round-trips **when the active profile is 4
  (Personalize)**.
- `put_GEQLevels` returns `0x8002000D DISP_E_ARRAYISLOCKED` under profile 0.
  Conclusion: **the GEQ is only editable in the Personalize profile**, matching
  how the Dolby Access UI behaves.

## Consequences

- The EQ editor must switch to Personalize before writing, and restore the
  previous profile afterwards. This is a product rule, not a workaround.
- `Initialize()` and `GetVersionNumber()` return `0x800706EF
  RPC_X_NULL_REF_POINTER`, yet every property access works. The instance appears
  to be pre-initialised by the service. `ss_open` therefore reports `PARTIAL`
  rather than failing. Do not treat `Initialize` failure as fatal.
- We copy a vendor DLL at runtime. Nothing is redistributed and no licensing
  check is bypassed - we load a component the user already owns, from a location
  the OS is willing to execute. The copy must be refreshed when the Dolby Access
  package version changes; `ss_open` should compare versions and re-copy.
- This is undocumented surface. A Dolby update can break it. The
  `IAudioBackend` abstraction and the Equalizer APO backend remain necessary.
- `RegisterCallback(IDAXEvent*)` exists and is not yet used. It is the path to a
  live UI that reflects changes made in Dolby Access itself.
