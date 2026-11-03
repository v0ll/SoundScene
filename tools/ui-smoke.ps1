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

    public static AutomationElement TabItem(AutomationElement scope, string name)
    {
        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
            new PropertyCondition(AutomationElement.NameProperty, name));
        return scope.FindFirst(TreeScope.Descendants, cond);
    }

    public static void Select(AutomationElement el)
    {
        ((SelectionItemPattern)el.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    public static int Count(AutomationElement scope, ControlType type)
    {
        var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, type);
        return scope.FindAll(TreeScope.Descendants, cond).Count;
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

function Get-BandCount {
    # The curve Dolby persisted is the only band count this script can read
    # without opening the DAX session the app already owns.
    $xml = Get-Content $runtimeXml -Raw
    if ($xml -match 'graphic-equalizer-bands"\s+value="([^"]+)"') {
        return ($Matches[1].Split(',')).Count
    }
    return 20
}

function Show-Popup {
    # A second launch is the app's own "show the popup" signal. Retry because a
    # secondary instance can exit before the running one processes the request.
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        Start-Process $Exe | Out-Null
        for ($i = 0; $i -lt 20; $i++) {
            Start-Sleep -Milliseconds 250
            $w = [Uia]::Window("SoundScene")
            if ($w) { return $w }
        }
    }
    throw "The popup did not appear."
}

function Wait-Button([object]$scope, [string]$name) {
    for ($i = 0; $i -lt 16; $i++) {
        $button = [Uia]::Button($scope, $name)
        if ($button) { return $button }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

function Wait-Profile([int]$expected) {
    # DAX flushes Runtime.xml asynchronously, so poll instead of reading once.
    for ($i = 0; $i -lt 12; $i++) {
        if ((Get-Profile) -eq $expected) { return $true }
        Start-Sleep -Milliseconds 250
    }
    return $false
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
    $gameOk = Wait-Profile 2
    "after Game    : $(Get-Profile)"
    if (-not $gameOk) { $failures += "Game did not activate (profile $(Get-Profile))" }

    Click-Profile "Movie"
    $movieOk = Wait-Profile 0
    "after Movie   : $(Get-Profile)"
    if (-not $movieOk) { $failures += "Movie did not activate (profile $(Get-Profile))" }

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

    $savedScene = (Get-Content $sceneFile -Raw | ConvertFrom-Json).Scenes |
        Where-Object { $_.Name -eq $sceneName }
    "scene profile : $($savedScene.Profile) index=$($savedScene.ProfileIndex)"
    if ($savedScene.Profile -ne "Movie" -or $savedScene.ProfileIndex -ne 0) {
        $failures += "The scene recorded $($savedScene.Profile) index $($savedScene.ProfileIndex) instead of Movie 0"
    }

    $w = Show-Popup
    $tile = Wait-Button $w $sceneName
    "scene tile    : $($null -ne $tile)"
    if ($tile) {
        [Uia]::Invoke($tile)
        $appliedOk = Wait-Profile 0
        "after apply   : $(Get-Profile)"
        if (-not $appliedOk) { $failures += "Applying the scene did not restore profile 0" }
    }
    else {
        $failures += "The scene tile never appeared"
    }

    $w = Show-Popup
    $delete = Wait-Button $w "Remove scene"
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

    # The EQ window must build itself from what the driver reports. Selecting
    # the Advanced tab materialises one slider per real band, so the count is a
    # direct check that 20 is not baked into the layout.
    $w = Show-Popup
    [Uia]::InvokeThreaded([Uia]::Button($w, "EQ"))

    # An owned dialog is merged into the popup's automation subtree rather than
    # appearing as a desktop root child.
    $eq = $null
    for ($i = 0; $i -lt 20 -and -not $eq; $i++) {
        Start-Sleep -Milliseconds 250
        $eq = [Uia]::Descendant($w, [System.Windows.Automation.ControlType]::Window, "Equalizer")
    }
    "eq window     : $($null -ne $eq)"
    if (-not $eq) {
        $failures += "The EQ button did not open the equalizer"
    }
    else {
        $advanced = [Uia]::TabItem($eq, "Advanced")
        if (-not $advanced) {
            $failures += "The equalizer has no Advanced tab"
        }
        else {
            [Uia]::Select($advanced)
            Start-Sleep -Milliseconds 700
            $sliders = [Uia]::Count($eq, [System.Windows.Automation.ControlType]::Slider)
            $expected = Get-BandCount
            "eq bands      : $sliders (driver $expected)"
            if ($sliders -ne $expected) {
                $failures += "The Advanced tab built $sliders sliders for $expected driver bands"
            }
        }

        [Uia]::Invoke([Uia]::Button($eq, "Close"))
        Start-Sleep -Milliseconds 500
    }

    # Opening the equalizer switches Dolby to Personalize, so put it back.
    Click-Profile "Movie"
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
