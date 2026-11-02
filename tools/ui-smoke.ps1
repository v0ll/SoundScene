<#
.SYNOPSIS
    Drives SoundScene through UI Automation: switch profiles, save a scene, apply it, delete it.

.DESCRIPTION
    This is the only test that exercises the app's real wiring - the popup's
    commands, the modal name prompt, the scene list. It needs Dolby Access and
    reads the persisted profile from Runtime.xml, because the running app holds
    the DAX client DLL and a second session cannot be opened.

    It refuses to run when the scene library is not empty: the delete step would
    otherwise be ambiguous and could remove a real scene.

    Start with nothing running. The script starts and stops the app itself.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\src\SoundScene.App\bin\Release\net10.0-windows10.0.26100.0\SoundScene.App.exe")
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System;
using System.Threading;
using System.Windows.Automation;

public static class Uia
{
    public static AutomationElement Window(string name)
    {
        var cond = new PropertyCondition(AutomationElement.NameProperty, name);
        return AutomationElement.RootElement.FindFirst(TreeScope.Children, cond);
    }

    public static AutomationElement Button(AutomationElement scope, string name)
    {
        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, name));
        return scope.FindFirst(TreeScope.Descendants, cond);
    }

    public static AutomationElement Edit(AutomationElement scope)
    {
        var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
        return scope.FindFirst(TreeScope.Descendants, cond);
    }

    public static AutomationElement Descendant(AutomationElement scope, ControlType type, string name)
    {
        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, type),
            new PropertyCondition(AutomationElement.NameProperty, name));
        return scope.FindFirst(TreeScope.Descendants, cond);
    }

    public static void Invoke(AutomationElement el)
    {
        ((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    // Commands that open a modal dialog do not return until it closes, so run
    // them on their own thread and drive the dialog from the caller.
    public static void InvokeThreaded(AutomationElement el)
    {
        var t = new Thread(() => Invoke(el));
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    public static void SetText(AutomationElement el, string text)
    {
        ((ValuePattern)el.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text);
    }
}
'@

$sceneFile = Join-Path $env:APPDATA "SoundScene\scenes.json"
$runtimeXml = "C:\ProgramData\Dolby\DAX3\Runtime.xml"
$sceneName = "ui-smoke-scene"
$failures = @()

if (Get-Process -Name "SoundScene.App" -ErrorAction SilentlyContinue) {
    throw "SoundScene is already running. Exit it first."
}

if ((Test-Path $sceneFile) -and (Get-Content $sceneFile -Raw) -notmatch '"Scenes":\s*\[\s*\]') {
    throw "The scene library is not empty. This test deletes its own scene and needs an unambiguous list."
}

if (-not (Test-Path $Exe)) {
    throw "Not built: $Exe"
}

function Get-Profile {
    $xml = Get-Content $runtimeXml -Raw
    if ($xml -match 'SelectedMainProfile value="(\w+)"') {
        switch ($Matches[1]) {
            "movie" { return 0 }
            "music" { return 1 }
            "game" { return 2 }
            "voice" { return 3 }
            "personalize" { return 4 }
            "dynamic" { return 5 }
        }
    }
    return -1
}

function Show-Popup {
    # A second launch is the app's own "show the popup" signal.
    Start-Process $Exe | Out-Null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        $w = [Uia]::Window("SoundScene")
        if ($w) { return $w }
    }
    throw "The popup did not appear."
}

function Click-Profile([string]$name) {
    $w = Show-Popup
    $b = [Uia]::Button($w, $name)
    if (-not $b) { throw "No '$name' button in the popup." }
    [Uia]::Invoke($b)
    Start-Sleep -Milliseconds 1200
}

$app = Start-Process $Exe -PassThru
try {
    Start-Sleep -Seconds 5

    $start = Get-Profile
    "start profile : $start"

    Click-Profile "Game"
    $game = Get-Profile
    "after Game    : $game"
    if ($game -ne 2) { $failures += "Game did not activate (profile $game)" }

    Click-Profile "Movie"
    $movie = Get-Profile
    "after Movie   : $movie"
    if ($movie -ne 0) { $failures += "Movie did not activate (profile $movie)" }

    $w = Show-Popup
    [Uia]::InvokeThreaded([Uia]::Button($w, "+ Save current"))

    # The owned prompt is merged into the popup's automation subtree.
    $edit = $null
    for ($i = 0; $i -lt 20 -and -not $edit; $i++) {
        Start-Sleep -Milliseconds 250
        $edit = [Uia]::Edit($w)
    }
    if (-not $edit) { throw "The name prompt did not appear." }

    [Uia]::SetText($edit, $sceneName)
    Start-Sleep -Milliseconds 300
    [Uia]::Invoke([Uia]::Button($w, "Save"))
    Start-Sleep -Milliseconds 1200

    $saved = (Test-Path $sceneFile) -and ((Get-Content $sceneFile -Raw) -match $sceneName)
    "scene saved   : $saved"
    if (-not $saved) { $failures += "The scene was not written to disk" }

    $w = Show-Popup
    $tile = [Uia]::Button($w, $sceneName)
    "scene tile    : $($null -ne $tile)"
    if ($tile) {
        [Uia]::Invoke($tile)
        Start-Sleep -Milliseconds 1200
        $applied = Get-Profile
        "after apply   : $applied"
        if ($applied -ne $movie) { $failures += "Applying the scene did not restore profile $movie" }
    }
    else {
        $failures += "The scene tile never appeared"
    }

    $w = Show-Popup
    $delete = [Uia]::Button($w, "Remove scene")
    "delete button : $($null -ne $delete)"
    if ($delete) {
        [Uia]::Invoke($delete)
        Start-Sleep -Milliseconds 400
        [Uia]::Invoke($delete)
        Start-Sleep -Milliseconds 800
    }

    $remaining = if (Test-Path $sceneFile) { Get-Content $sceneFile -Raw } else { "" }
    $removed = $remaining -notmatch $sceneName
    "scene removed : $removed"
    if (-not $removed) { $failures += "The scene survived two delete clicks" }

    # The header button must reach the same editor as the tray menu.
    $w = Show-Popup
    [Uia]::InvokeThreaded([Uia]::Button($w, "Hotkeys"))
    $hotkeys = $null
    for ($i = 0; $i -lt 20 -and -not $hotkeys; $i++) {
        Start-Sleep -Milliseconds 250
        $hotkeys = [Uia]::Descendant([System.Windows.Automation.AutomationElement]::RootElement,
            [System.Windows.Automation.ControlType]::Window, "Hotkeys")
    }
    "hotkeys dialog: $($null -ne $hotkeys)"
    if (-not $hotkeys) {
        $failures += "The Hotkeys button did not open the editor"
    }
    else {
        $add = [Uia]::Button($hotkeys, "+ Add hotkey")
        if (-not $add) { $failures += "The hotkey editor has no Add button" }
        [Uia]::Invoke([Uia]::Button($hotkeys, "Cancel"))
        Start-Sleep -Milliseconds 400
    }
}
finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -eq 0) {
    "RESULT        : PASS"
}
else {
    "RESULT        : FAIL"
    $failures | ForEach-Object { "  - $_" }
    exit 1
}
