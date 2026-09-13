#define AppVersion "0.4.1"
[Setup]
AppId={{C031CF79-A16D-4975-8581-5CC3C736CE85}
AppName=Pame
AppVersion={#AppVersion}
AppPublisher=Pame
AppPublisherURL=https://github.com/LielZ/Pame
AppSupportURL=https://github.com/LielZ/Pame/issues
AppUpdatesURL=https://github.com/LielZ/Pame/releases
DefaultDirName={localappdata}\Programs\Pame
DefaultGroupName=Pame
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Pame-Setup-{#AppVersion}-x64
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Pame.exe
SetupIconFile=..\src\Pame.App\Assets\pame.ico
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion=0.4.1.0

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: checkedonce
Name: "startup"; Description: "Start Pame after signing in to Windows"; Flags: unchecked
Name: "serviceaccess"; Description: "Windows service access (one administrator confirmation during setup; no prompts during games)"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "runtimes\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsWebView2

[Icons]
Name: "{autoprograms}\Pame"; Filename: "{app}\Pame.exe"
Name: "{autoprograms}\Pame Recovery"; Filename: "{app}\Pame.exe"; Parameters: "--safe-mode"
Name: "{autodesktop}\Pame"; Filename: "{app}\Pame.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Pame"; ValueData: """{app}\Pame.exe"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Preparing Pame browser..."; Flags: runhidden waituntilterminated; Check: NeedsWebView2
Filename: "{app}\Pame.exe"; Parameters: "--install-service-access"; Tasks: serviceaccess; Flags: runhidden waituntilterminated
Filename: "{app}\Pame.exe"; Description: "Launch Pame"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsWebView2: Boolean;
var Version: String;
begin
  Result := True;
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    if (Version <> '') and (Version <> '0.0.0.0') then Result := False;
  if RegQueryStringValue(HKCU32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    if (Version <> '') and (Version <> '0.0.0.0') then Result := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Value: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Pame', Value) then
      if Pos(ExpandConstant('{app}\Pame.exe'), Value) > 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Pame');
end;
