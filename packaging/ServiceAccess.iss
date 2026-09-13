#define AppVersion "0.3.3"
[Setup]
AppId={{D5D86975-45AF-4C74-A2B5-30DDB742E133}
AppName=Pame Windows service access
AppVersion={#AppVersion}
AppPublisher=Pame
DefaultDirName={commonpf64}\Pame\ServiceAccess
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Pame-ServiceAccess-Setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Pame.App\Assets\pame.ico
UninstallDisplayIcon={app}\Pame.ServiceAccess.exe
CloseApplications=no
SetupLogging=yes
VersionInfoVersion=0.3.3.0

[Files]
Source: "..\dist\service\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Pame\ServiceAccess"; ValueType: string; ValueName: "OwnerSid"; ValueData: "{param:OWNER}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Pame\ServiceAccess"; ValueType: string; ValueName: "ClientPath"; ValueData: "{param:CLIENT}"

[UninstallRun]
Filename: "{app}\Pame.ServiceAccess.exe"; Parameters: "--unregister"; Flags: runhidden waituntilterminated

[Code]
function InitializeSetup(): Boolean;
begin
  Result := (Pos('S-1-', ExpandConstant('{param:OWNER}')) = 1) and (ExtractFileName(ExpandConstant('{param:CLIENT}')) = 'Pame.exe');
  if not Result then MsgBox('Run this optional add-on from the Pame installer.', mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Pame.ServiceAccess.exe')) then
    if not Exec(ExpandConstant('{app}\Pame.ServiceAccess.exe'), '--unregister', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      Result := 'Pame could not restore and stop its previous service. Close the game and retry.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then
    if not Exec(ExpandConstant('{app}\Pame.ServiceAccess.exe'), '--register', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException('The optional Windows service could not be registered. Regular Pame games remain available.');
end;
