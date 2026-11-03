# Verifying a download

Release assets are built by GitHub Actions from the tagged commit, not on a personal machine. Two
independent checks are available.

## 1. Build provenance

Install the GitHub CLI (`winget install GitHub.cli`), then:

```powershell
gh attestation verify .\SoundScene-Setup-0.2.3.exe --repo v0ll/SoundScene
gh attestation verify .\SoundScene-Portable-0.2.3.zip --repo v0ll/SoundScene
```

A pass means GitHub's build service observed this repository's release workflow producing that
exact file at that tag. This is stronger than a checksum stored on the same release page, which
could be replaced together with the file.

## 2. SHA-256

```powershell
Get-FileHash .\SoundScene-Setup-0.2.3.exe -Algorithm SHA256
Get-FileHash .\SoundScene-Portable-0.2.3.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

Each hash must match the corresponding line. GitHub also shows an asset digest on the release page.

## 3. Scan the file

Upload the downloaded file to [VirusTotal](https://www.virustotal.com/gui/home/upload) and compare
the SHA-256 on the report with `SHA256SUMS.txt`. Scanning the exact bytes you have is the only
version of this check that means anything: a scan of a different build has a different hash.

Published scans of the current release:
[setup](https://www.virustotal.com/gui/file/c54646a1c68a06da24ee81acc691cd1699e0dd87bf471f14ee22f0f51695d85a/detection) ·
[portable](https://www.virustotal.com/gui/file/6b6ac7e536eb846ab5d79de005f1c01310c710b13ac51fe51d24e41284ee70e0/detection).

Unsigned installers that write a Run key and copy DLLs commonly draw one or two generic heuristic
detections. If an engine flags it, open the detection and use that vendor's false-positive form.
For Microsoft, use <https://www.microsoft.com/en-us/wdsi/filesubmission> and select "Software
developer".

## 4. Build it yourself

The app has no NuGet dependencies and no network code. With the .NET 10 SDK and the Visual Studio
C++ build tools:

```powershell
git clone https://github.com/v0ll/SoundScene
cd SoundScene
cmake -S native\SoundScene.DaxBridge -B native/build -G "Visual Studio 17 2022" -A x64
cmake --build native/build --config Release
dotnet publish src\SoundScene.App\SoundScene.App.csproj -c Release -r win-x64 --self-contained true -o out
```

`out\SoundScene.App.exe` is then your own build.

## SmartScreen

The installer is not code-signed, so Windows shows an unknown publisher and may show "Windows
protected your PC". Verify the file with the commands above, then choose **More info > Run anyway**.
The application does not ask you to disable Defender, add exclusions, or run as administrator.
