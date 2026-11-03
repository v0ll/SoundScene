#ifndef AppVersion
  #define AppVersion "0.2.2"
#endif

#ifndef SourceDir
  #define SourceDir "..\build\release\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{8E4C7A52-2F5B-4B8E-9F0A-6C3D2A1B7C44}
AppName=SoundScene
AppVersion={#AppVersion}
AppVerName=SoundScene {#AppVersion}
AppPublisher=Ali Eren Altuntas
AppPublisherURL=https://github.com/v0ll/SoundScene
AppSupportURL=https://github.com/v0ll/SoundScene/issues
AppUpdatesURL=https://github.com/v0ll/SoundScene/releases
DefaultDirName={localappdata}\Programs\SoundScene
DefaultGroupName=SoundScene
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=SoundScene-Setup-{#AppVersion}
SetupIconFile=..\src\SoundScene.App\Assets\SoundScene.ico
UninstallDisplayIcon={app}\SoundScene.App.exe
UninstallDisplayName=SoundScene
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=yes
RestartApplications=no
AllowNoIcons=yes
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startup"; Description: "Start SoundScene with &Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Older script-based installs put the shortcut directly in Programs, without
; the group folder Inno uses. Remove it so upgrades do not leave two icons.
Type: files; Name: "{userappdata}\Microsoft\Windows\Start Menu\Programs\SoundScene.lnk"

[Icons]
Name: "{group}\SoundScene"; Filename: "{app}\SoundScene.App.exe"
Name: "{group}\Uninstall SoundScene"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SoundScene"; Filename: "{app}\SoundScene.App.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SoundScene"; ValueData: """{app}\SoundScene.App.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\SoundScene.App.exe"; Description: "Launch SoundScene"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "{userappdata}\Microsoft\Windows\Start Menu\Programs\SoundScene.lnk"

[Code]
var
  RequirementsPage: TWizardPage;
  RequirementsText: TNewStaticText;
  StoreButton: TNewButton;
  RecheckButton: TNewButton;
  KeepSettings: Boolean;
  ResetDolby: Boolean;

function StartsWith(const Value, Prefix: String): Boolean;
begin
  Result := Copy(Value, 1, Length(Prefix)) = Prefix;
end;

function HasPackage(const Prefix: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;

  if RegGetSubkeyNames(
       HKCU,
       'Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages',
       Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if StartsWith(Names[I], Prefix) then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function DolbyAccessInstalled(): Boolean;
begin
  Result := HasPackage('DolbyLaboratories.DolbyAccess_');
end;

function VcLibsInstalled(): Boolean;
begin
  Result := HasPackage('Microsoft.VCLibs.140.00_');
end;

function DaxServiceInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\DolbyDAXAPI');
end;

function RequirementReport(): String;
begin
  Result := 'Windows 10 2004 or newer: [OK]' + #13#10;

  if DolbyAccessInstalled() then
    Result := Result + 'Dolby Access: [OK]' + #13#10
  else
    Result := Result + 'Dolby Access: [MISSING] install it free from the Store' + #13#10;

  if VcLibsInstalled() then
    Result := Result + 'Microsoft VCLibs: [OK]' + #13#10
  else
    Result := Result + 'Microsoft VCLibs: [MISSING] installed with Dolby Access' + #13#10;

  if DaxServiceInstalled() then
    Result := Result + 'Dolby DAX service: [OK]'
  else
    Result := Result + 'Dolby DAX service: [MISSING] install the ASUS Dolby Atmos driver';
end;

procedure UpdateRequirements();
begin
  RequirementsText.Caption := RequirementReport();
end;

procedure OpenStore(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', 'ms-windows-store://pdp/?productid=9N0866FS04W8', '',
    '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure Recheck(Sender: TObject);
begin
  UpdateRequirements();
end;

procedure InitializeWizard();
var
  Intro: TNewStaticText;
  Note: TNewStaticText;
begin
  RequirementsPage := CreateCustomPage(
    wpWelcome,
    'Requirements',
    'Check that this PC is ready for SoundScene.');

  Intro := TNewStaticText.Create(RequirementsPage);
  Intro.Parent := RequirementsPage.Surface;
  Intro.Left := 0;
  Intro.Top := 0;
  Intro.Width := RequirementsPage.SurfaceWidth;
  Intro.Height := 46;
  Intro.AutoSize := False;
  Intro.WordWrap := True;
  Intro.Caption :=
    'SoundScene controls Dolby Access; it does not include or install Dolby software. ' +
    'The ROG audio driver alone is not enough.';

  RequirementsText := TNewStaticText.Create(RequirementsPage);
  RequirementsText.Parent := RequirementsPage.Surface;
  RequirementsText.Left := 0;
  RequirementsText.Top := 54;
  RequirementsText.Width := RequirementsPage.SurfaceWidth;
  RequirementsText.Height := 96;
  RequirementsText.AutoSize := False;
  RequirementsText.WordWrap := True;

  StoreButton := TNewButton.Create(RequirementsPage);
  StoreButton.Parent := RequirementsPage.Surface;
  StoreButton.Left := 0;
  StoreButton.Top := 160;
  StoreButton.Width := 190;
  StoreButton.Height := 26;
  StoreButton.Caption := 'Open Microsoft Store';
  StoreButton.OnClick := @OpenStore;

  RecheckButton := TNewButton.Create(RequirementsPage);
  RecheckButton.Parent := RequirementsPage.Surface;
  RecheckButton.Left := 198;
  RecheckButton.Top := 160;
  RecheckButton.Width := 110;
  RecheckButton.Height := 26;
  RecheckButton.Caption := 'Recheck';
  RecheckButton.OnClick := @Recheck;

  Note := TNewStaticText.Create(RequirementsPage);
  Note.Parent := RequirementsPage.Surface;
  Note.Left := 0;
  Note.Top := 198;
  Note.Width := RequirementsPage.SurfaceWidth;
  Note.Height := 44;
  Note.AutoSize := False;
  Note.WordWrap := True;
  Note.Caption :=
    'You can install SoundScene now and add Dolby Access later. ' +
    'The app will show this checklist again from the tray menu.';

  UpdateRequirements();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  if CurPageID = RequirementsPage.ID then
    UpdateRequirements();

  // Missing prerequisites are a warning, not a blocker: Dolby Access can be
  // installed after SoundScene, and diagnostics may still be useful.
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/IM SoundScene.App.exe /F', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if UninstallSilent then
  begin
    KeepSettings := False;
    ResetDolby := False;
  end
  else
  begin
    KeepSettings := MsgBox(
      'Delete your SoundScene settings and scenes?' + #13#10 + #13#10 +
      'Choose No to keep them for a future reinstall.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDNO;

    ResetDolby := MsgBox(
      'Also reset the Dolby EQ, volume leveler, and surround virtualizer to flat/off?' + #13#10 + #13#10 +
      'Choose No to leave your current Dolby settings untouched.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;

  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if ResetDolby then
      Exec(
        ExpandConstant('{app}\SoundScene.App.exe'),
        '--reset-dolby',
        ExpandConstant('{app}'),
        SW_HIDE,
        ewWaitUntilTerminated,
        ResultCode);

    RegDeleteValue(HKCU,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'SoundScene');

    // The DAX cache is ours regardless of the settings choice.
    DelTree(ExpandConstant('{localappdata}\SoundScene'), True, True, True);

    if not KeepSettings then
      DelTree(ExpandConstant('{userappdata}\SoundScene'), True, True, True);
  end;
end;
