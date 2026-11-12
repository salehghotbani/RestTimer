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
ShowLanguageDialog=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription=RestTimer Installer

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fa"; MessagesFile: "compiler:Default.isl,languages\Farsi.isl"

[CustomMessages]
en.Autostart=Run RestTimer when I sign in
fa.Autostart=اجرای RestTimer هنگام ورود به ویندوز
en.DesktopIcon=Create a desktop shortcut
fa.DesktopIcon=ساخت میانبر روی دسکتاپ
en.LaunchApp=Run RestTimer
fa.LaunchApp=اجرای RestTimer
en.DotNetRequired=Install Microsoft .NET Framework 4.8, then run this installer again.
fa.DotNetRequired=ابتدا Microsoft .NET Framework 4.8 را نصب کنید و سپس این نصاب را دوباره اجرا کنید.

[Tasks]
Name: "autostart"; Description: "{cm:Autostart}"; Flags: unchecked
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

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
Filename: "{app}\RestTimer.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    MsgBox(CustomMessage('DotNetRequired'), mbError, MB_OK);
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
