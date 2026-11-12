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
AppMutex={code:GetAppMutex}
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
CloseApplicationsFilter=RestTimer.exe
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
en.CloseForInstall=RestTimer is running. Setup will close it automatically and reopen it after installation.
fa.CloseForInstall=برنامه باز است. برای ادامهٔ نصب، آن را می‌بندم و پس از پایان نصب دوباره اجرا می‌کنم.
en.CloseFailed=RestTimer could not be closed automatically. Close the program and try again.
fa.CloseFailed=بستن خودکار RestTimer ممکن نشد. برنامه را ببندید و دوباره تلاش کنید.
en.RestartFailed=RestTimer could not be reopened. You can start it from the Start menu.
fa.RestartFailed=اجرای مجدد RestTimer ممکن نشد. می‌توانید آن را از منوی Start اجرا کنید.

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
Filename: "{app}\RestTimer.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: OfferLaunch

[Code]
var
  RestorePending: Boolean;
  InstallationSucceeded: Boolean;
  CloseNoticeShown: Boolean;

function GetAppMutex(Param: String): String;
begin
  if IsUninstaller then
    Result := 'Local\RestTimer.Singleton'
  else
    Result := '';
end;

function OfferLaunch(): Boolean;
begin
  Result := not RestorePending;
end;

procedure RestoreApplication();
var
  Path: String;
  ErrorCode: Integer;
begin
  if not RestorePending then
    Exit;
  RestorePending := False;
  Path := ExpandConstant('{app}\RestTimer.exe');
  if not InstallationSucceeded then
    Path := GetIniString('State', 'Executable', Path, ExpandConstant('{tmp}\resttimer-closed.ini'));
  if FileExists(Path) then
    if Exec(Path, '', ExtractFileDir(Path), SW_SHOWNORMAL, ewNoWait, ErrorCode) then
      Exit;
  Log(CustomMessage('RestartFailed'));
  if not WizardSilent then
    MsgBox(CustomMessage('RestartFailed'), mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Parameters: String;
begin
  Result := '';
  if not CheckForMutexes('Local\RestTimer.Singleton') then
    Exit;
  if not CloseNoticeShown then begin
    Log(CustomMessage('CloseForInstall'));
    if not WizardSilent then
      MsgBox(CustomMessage('CloseForInstall'), mbInformation, MB_OK);
    CloseNoticeShown := True;
  end;
  RestorePending := True;
  ExtractTemporaryFile('RestTimer.exe');
  Parameters := '--close-for-install "' + ExpandConstant('{tmp}\resttimer-closed.ini') + '"';
  if not Exec(ExpandConstant('{tmp}\RestTimer.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
    Result := CustomMessage('CloseFailed');
    Exit;
  end;
  if ((ResultCode <> 0) and (ResultCode <> 10)) or CheckForMutexes('Local\RestTimer.Singleton') then
    Result := CustomMessage('CloseFailed');
end;

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
  if CurStep = ssPostInstall then
    InstallationSucceeded := True;
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RestTimer');
  if CurStep = ssDone then
    RestoreApplication();
end;

procedure DeinitializeSetup();
begin
  RestoreApplication();
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
