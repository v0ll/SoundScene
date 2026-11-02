<#
.SYNOPSIS
    Publishes SoundScene to %LOCALAPPDATA%\Programs\SoundScene and creates a Start Menu shortcut.

.DESCRIPTION
    Installs the app for the current user only, so nothing needs elevation:

      %LOCALAPPDATA%\Programs\SoundScene\SoundScene.App.exe
      %APPDATA%\Microsoft\Windows\Start Menu\Programs\SoundScene.lnk

    The native bridge must be built first, because dotnet publish alone cannot
    produce it (it is compiled out-of-band by CMake):

      cmake --build native\build --config Release

    A running copy is closed before publishing: it holds the bridge DLL open and
    the copy would fail.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\install.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Desktop
#>
param(
    [switch]$Desktop
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$bridge = Join-Path $root "native\build\Release\SoundScene.DaxBridge.dll"
$target = Join-Path $env:LOCALAPPDATA "Programs\SoundScene"
$exe = Join-Path $target "SoundScene.App.exe"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SoundScene.lnk"

if (-not (Test-Path $bridge)) {
    throw "The native bridge is missing. Build it first: cmake --build native\build --config Release"
}

Get-Process -Name "SoundScene*" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}

& dotnet publish (Join-Path $root "src\SoundScene.App") -c Release -o $target
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $exe)) {
    throw "Publish finished but $exe is missing."
}

# publish copies the build output it knows about, and the bridge is not an item,
# so Directory.Build.targets places it next to the exe. Verify it actually did.
$publishedBridge = Join-Path $target "SoundScene.DaxBridge.dll"
if (-not (Test-Path $publishedBridge)) {
    throw "The published folder has no SoundScene.DaxBridge.dll; the app cannot open DAX."
}

function New-Shortcut([string]$path) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $target
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = "SoundScene - Dolby profiles, EQ and scenes"
    $shortcut.Save()
}

New-Shortcut $startMenu

if ($Desktop) {
    New-Shortcut (Join-Path ([Environment]::GetFolderPath("Desktop")) "SoundScene.lnk")
}

Write-Host "Installed:"
Write-Host "  exe       $exe"
Write-Host "  shortcut  $startMenu"
if ($Desktop) {
    Write-Host "  desktop   SoundScene.lnk"
}
Write-Host ""
Write-Host "Launch it from the Start Menu, or pin the shortcut to the taskbar."
Write-Host "Turn on 'Start with Windows' from the tray menu to keep it running."
Write-Host ""
Write-Host "This is the development install. To remove it:"
Write-Host "  powershell -ExecutionPolicy Bypass -File tools\uninstall.ps1"
