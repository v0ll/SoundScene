<#
.SYNOPSIS
    Builds the self-contained app, the portable ZIP, and the Inno Setup installer.

.DESCRIPTION
    This is the release build. It publishes win-x64 self-contained so users do
    not need the .NET runtime, adds the native DAX bridge, writes SHA-256 hashes,
    and compiles the per-user installer.

    Inno Setup 6 is required and is not installed by this script:
      winget install JRSoftware.InnoSetup

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Version 0.3.0
#>
param(
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root "build\release"
$publish = Join-Path $build "publish"
$portable = Join-Path $build "portable\SoundScene"
$dist = Join-Path $root "dist"
$bridge = Join-Path $root "native\build\Release\SoundScene.DaxBridge.dll"
$appProject = Join-Path $root "src\SoundScene.App\SoundScene.App.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
    $Version = $props.Project.PropertyGroup.VersionPrefix | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine the version. Pass -Version."
}

Write-Host "SoundScene $Version"
Write-Host ""

# The native bridge is compiled out-of-band and publish only copies it if it
# already exists, so build it first and fail early if it is missing.
if (-not (Test-Path $bridge)) {
    Write-Host "Building the native DAX bridge..."
    $nativeBuild = Join-Path $root "native\build"
    if (-not (Test-Path (Join-Path $nativeBuild "CMakeCache.txt"))) {
        & cmake -S (Join-Path $root "native") -B $nativeBuild -G "Visual Studio 17 2022" -A x64
        if ($LASTEXITCODE -ne 0) { throw "CMake configure failed." }
    }

    & cmake --build $nativeBuild --config $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Native build failed." }
}

if (-not (Test-Path $bridge)) {
    throw "The native bridge is still missing: $bridge"
}

# A clean output keeps a stale bridge or PDB out of the installer.
Remove-Item $build -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publish -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null

Write-Host "Publishing self-contained win-x64..."
& dotnet publish $appProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedBridge = Join-Path $publish "SoundScene.DaxBridge.dll"
if (-not (Test-Path $publishedBridge)) {
    throw "The published folder has no SoundScene.DaxBridge.dll."
}

foreach ($doc in @("LICENSE.md", "NOTICE.md", "README.md")) {
    $source = Join-Path $root $doc
    if (Test-Path $source) {
        Copy-Item $source (Join-Path $publish $doc) -Force
    }
}

Write-Host "Building the portable ZIP..."
New-Item -ItemType Directory -Path $portable -Force | Out-Null
Copy-Item (Join-Path $publish "*") $portable -Recurse -Force

@"
This is a portable copy of SoundScene.

Settings, scenes, and the private Dolby client cache live in the data folder
next to SoundScene.App.exe. Nothing is written to %APPDATA% or %LOCALAPPDATA%,
and "Start with Windows" is disabled so the folder stays self-contained.

To uninstall, close SoundScene and delete this whole folder.
"@ | Set-Content (Join-Path $portable "portable.txt") -Encoding UTF8

$portableZip = Join-Path $dist "SoundScene-Portable-$Version.zip"
Compress-Archive -Path $portable -DestinationPath $portableZip -CompressionLevel Optimal

Write-Host "Building the installer..."
$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 is required. Install it with: winget install JRSoftware.InnoSetup"
}

& $iscc (Join-Path $root "installer\SoundScene.iss") `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publish" `
    "/DOutputDir=$dist"
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

Write-Host "Hashing artifacts..."
$artifacts = Get-ChildItem $dist -File | Where-Object { $_.Extension -in @('.exe', '.zip') }
$hashes = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
$hashes | Set-Content (Join-Path $dist "SHA256SUMS.txt") -Encoding ASCII

Write-Host ""
Write-Host "Artifacts in ${dist}:"
Get-ChildItem $dist -File | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
Write-Host "These artifacts are unsigned. Publish SHA256SUMS.txt with them."
