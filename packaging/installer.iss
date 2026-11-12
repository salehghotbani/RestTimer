#ifndef AppVersion
  #error AppVersion is required
#endif

[Setup]
AppId={{77527277-48F0-4BDF-B74C-99A026A3B8C5}
AppName=RestTimer
AppVersion={#AppVersion}
AppPublisher=Saleh Ghotbani
AppPublisherURL=https://github.com/salehghotbani/RestTimer
AppSupportURL=https://github.com/salehghotbani/RestTimer/issues
AppMutex=Local\RestTimer.Singleton
DefaultDirName={localappdata}\Programs\RestTimer
DefaultGroupName=RestTimer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist\release
OutputBaseFilename=RestTimer-v{#AppVersion}-windows-x64-setup
SetupIconFile=..\RestTimer.ico
UninstallDisplayIcon={app}\RestTimer.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription=RestTimer Installer

[Tasks]
Name: "autostart"; Description: "Run RestTimer when I sign in"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\Windows\RestTimer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Windows\RestTimer.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\RestTimer"; Filename: "{app}\RestTimer.exe"
Name: "{autodesktop}\RestTimer"; Filename: "{app}\RestTimer.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "RestTimer"; ValueData: """{app}\RestTimer.exe"""; Tasks: autostart

[Run]
Filename: "{app}\RestTimer.exe"; Description: "Run RestTimer"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    MsgBox('Install Microsoft .NET Framework 4.8, then run this installer again.', mbError, MB_OK);
end;

procedure InitializeWizard();
var
  StartupCommand: String;
begin
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RestTimer', StartupCommand) then
    WizardSelectTasks('autostart');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RestTimer');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RestTimer', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\RestTimer.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RestTimer');
end;
