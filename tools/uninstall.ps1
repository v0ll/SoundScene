<#
.SYNOPSIS
    Removes a script-installed SoundScene and everything it wrote.

.DESCRIPTION
    This is the uninstaller for tools\install.ps1, not for the Inno Setup
    package (that ships its own uninstaller under Settings > Apps).

    It stops the app, removes the install folder, shortcuts, the Start with
    Windows entry, settings/scenes, and the copied Dolby client cache. Dolby
    Access, its driver, and C:\ProgramData\Dolby\DAX3\Runtime.xml are left alone
    unless -ResetDolby is passed.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\uninstall.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\uninstall.ps1 -KeepSettings
#>
param(
    [switch]$KeepSettings,
    [switch]$ResetDolby
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA "Programs\SoundScene"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SoundScene.lnk"
$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "SoundScene.lnk"
$appData = Join-Path $env:APPDATA "SoundScene"
$localData = Join-Path $env:LOCALAPPDATA "SoundScene"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Get-Process -Name "SoundScene*" -ErrorAction SilentlyContinue | Stop-Process -Force

if ($ResetDolby -and (Test-Path (Join-Path $installDir "SoundScene.App.exe"))) {
    Write-Host "Resetting Dolby EQ, leveler, and surround..."
    Start-Process (Join-Path $installDir "SoundScene.App.exe") `
        -ArgumentList "--reset-dolby" -Wait -WindowStyle Hidden
}

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed $installDir"
}

foreach ($shortcut in @($startMenu, $desktop)) {
    if (Test-Path $shortcut) {
        Remove-Item $shortcut -Force
        Write-Host "Removed $shortcut"
    }
}

if (Test-Path $runKey) {
    Remove-ItemProperty -Path $runKey -Name "SoundScene" -ErrorAction SilentlyContinue
    Write-Host "Removed the Start with Windows entry"
}

if (-not $KeepSettings -and (Test-Path $appData)) {
    Remove-Item $appData -Recurse -Force
    Write-Host "Removed settings and scenes in $appData"
}

# The cache is a copy made from Dolby Access, not user data, so it always goes.
if (Test-Path $localData) {
    Remove-Item $localData -Recurse -Force
    Write-Host "Removed the Dolby client cache in $localData"
}

Write-Host ""
Write-Host "SoundScene was removed. Dolby Access, its driver, and Dolby's own"
Write-Host "Runtime.xml were not touched."
