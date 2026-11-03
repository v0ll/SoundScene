# DAX investigation notes

Live record of what we have verified about the Dolby Access / DAX 3 stack on this machine.
Everything here is observation, not documentation - treat it as provisional.

Machine: ASUS ROG Zephyrus G14 GA403UV, Windows 11, Dolby Access `3.27.12250.0`.

---

## 1. Installed components

| Component | Path |
| --- | --- |
| Dolby Access package | `C:\Program Files\WindowsApps\DolbyLaboratories.DolbyAccess_3.27.12250.0_x64__rz1tebttyb220\` |
| WinRT client DLL | `...\DAXRPCClient.dll` (exposes `DAX.CDolbyDAX`) |
| WinRT metadata | `...\DAX.winmd` |
| DAX service binary | `C:\WINDOWS\System32\DriverStore\FileRepository\dax3_swc_aposvc.inf_amd64_8e54c1bc7d581ad1\DAX3API.exe` |
| Default config | `...\dax3_swc_aposvc.inf_amd64_...\default_settings.json` |
| Live runtime state | `C:\ProgramData\Dolby\DAX3\Runtime.xml` |

- Package family: `DolbyLaboratories.DolbyAccess_rz1tebttyb220`
- DAX version: `3.30705.750.0`
- Service `DolbyDAXAPI` runs as **LocalSystem** from `DAX3API.exe`.

## 2. Surface exposed by `DAX.CDolbyDAX`

Properties:

- `ActiveProfile`, `ActiveSubProfile`
- `DolbyEnable`
- `GEQLevels` (20 bands on the tested G14; the array length is the real width)
- `IEQ`
- `SurroundVirtualizerEnable` / `SurroundVirtualizerValue`
- `DialogEnhancerEnable` / `DialogEnhancerValue`
- `VolumeLevelerEnable` / `VolumeLevelerValue`
- `BassEnhancerEnable`

Methods:

- `GetProperty`, `SetProperty`, `GetPropertyEx`, `SetPropertyEx`
- `GetHotkey`, `SetHotkey`
- `DaxEndpoints`, plus endpoint query APIs (`GetEndpointData`, ...)
- `GetGEQMaximumRange()`
- `get_GEQLevels(width, values)` and `get_DeviceModeCount` / `GetDeviceModeName`, used at startup to
  discover the band count and profile catalog instead of assuming the G14's

## 3. Open questions

| # | Question | Status |
| --- | --- | --- |
| 1 | Can a non-packaged process activate `DAX.CDolbyDAX`? | **unverified** |
| 2 | Does the `DolbyLabs.dolbyDAX3ApiService_rz1tebttyb220` capability gate callers? | **unverified** |
| 3 | What is the integer mapping for `ActiveProfile`? | guess: Movie 0, Music 1, Game 2, Voice 3, Personalize 4, Dynamic 5 |
| 4 | What is the real scale of `GEQLevels`? `GetGEQMaximumRange()` returns `192`. | **uncalibrated** |
| 5 | Which endpoint string format does the API accept? | candidates below |
| 6 | Is a write to `ActiveProfile` durable across reboot / Dolby Access restart? | **unverified** |
| 7 | Does writing `Runtime.xml` directly work, or is it overwritten by the service? | **unverified** |

Endpoint string candidates, in order of preference:

1. `{0.0.0.00000000}.{GUID}` (classic MMDevice ID)
2. Full `SWD\MMDEVAPI\...` device instance ID
3. Whatever `DaxEndpoints` / `GetEndpointData` returns

## 4. Activation strategy for the spike

1. Try `RoGetActivationFactory(L"DAX.CDolbyDAX", ...)` from a plain desktop process.
2. If that fails, `LoadLibraryExW` on `DAXRPCClient.dll` + `DllGetActivationFactory`.
3. If both fail, fall back to the app-service protocol
   (`GetProfile`, `SetProfile`, `SyncProfile`, `GetRuntimeParameters`, `GetConfigurations`).
4. Last resort, and a separate product decision: UI Automation against
   `DolbyLaboratories.DolbyAccess_rz1tebttyb220!App`.

## 5. Current observed state

`Runtime.xml` at time of audit:

- `SelectedMainProfile="movie"`
- profiles present: `movie`, `music`, `personalize`

## 6. Safety

- **Back up `Runtime.xml` before every experiment.** A bad write can leave the machine silent.
- Never write to `ProgramData` from a test that runs in CI.
- Do not redistribute anything from `WindowsApps`.

## 7. Prior art

- `morluto/rea` - reverse-engineering framework, not a switcher. Not needed for the first attempt.
- `MrHope1/DolbyAtmosSwitcher` - UI Automation approach. Launches Dolby Access, waits up to 8s,
  no global hotkey, no releases.
- `SpatialSwitch` - only toggles the Windows spatial-sound flag, not Dolby EQ profiles.
- G-Helper issue #6090 requested Dolby profile control; closed as out of scope.
