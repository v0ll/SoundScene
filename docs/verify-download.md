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

## 3. Build it yourself

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
