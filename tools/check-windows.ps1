$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    function Run([scriptblock]$Command) { & $Command; if($LASTEXITCODE -ne 0) {throw 'Local check failed.'} }
    $env:DOTNET_CLI_HOME=Join-Path (Get-Location) 'build/dotnet-home'
    if(!$env:ISCC) {
        $inno=Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*,HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue | Where-Object DisplayName -like '*Inno Setup*' | Select-Object -First 1
        if($inno.InstallLocation) { $env:ISCC=Join-Path $inno.InstallLocation 'ISCC.exe' }
    }
    Run { ./tools/build_windows.ps1 }
    Run { dotnet build tests/updates/WindowsUpdateTests.csproj -c Release -o obj/update-tests }
    Run { ./obj/update-tests/WindowsUpdateTests.exe }
    . ./tools/github.ps1
    New-Item -ItemType Directory -Force build/legacy | Out-Null
    if(!(Get-ChildItem build/legacy/*setup.exe -ErrorAction SilentlyContinue)) { Invoke-RestTimerGh -Arguments @('release','download','v1.3.1','--repo','salehghotbani/RestTimer','--pattern','*windows-x64-setup.exe','--dir','build/legacy') }
    # Installer smoke tests use a temporary directory but share Inno's per-user registration.
    # Preserve the existing installation registration, shortcut and updater preferences too.
    $savedFiles=@{}
    foreach($file in @((Join-Path ([Environment]::GetFolderPath('Programs')) 'RestTimer/RestTimer.lnk'),(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RestTimer/updates/state.json'))) {
        $savedFiles[$file]=if(Test-Path -LiteralPath $file) {[IO.File]::ReadAllBytes($file)} else {$null}
    }
    $backup=@{}
    $existed=@{}
    foreach($path in @('Software\RestTimer','Software\Microsoft\Windows\CurrentVersion\Run','Software\Microsoft\Windows\CurrentVersion\Uninstall\{77527277-48F0-4BDF-B74C-99A026A3B8C5}_is1')) {
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
        $existed[$path]=$null -ne $key
        $values=@{}
        if($key) { foreach($name in $key.GetValueNames()) {if($path -ne 'Software\Microsoft\Windows\CurrentVersion\Run' -or $name -eq 'RestTimer') {$values[$name]=@($key.GetValue($name),$key.GetValueKind($name))}};$key.Dispose() }
        $backup[$path]=$values
    }
    try {
        Run { ./tests/windows_localization.ps1 -TestPersistence }
        Run { ./tests/windows_dashboard.ps1 }
        Run { ./tests/windows_appearance.ps1 }
        Run { ./tests/windows_visual.ps1 }
        $legacy=(Get-ChildItem build/legacy/*setup.exe | Select-Object -First 1).FullName
        Run { ./tests/windows_smoke.ps1 -Language en -LegacyInstaller $legacy }
        Run { ./tests/windows_smoke.ps1 -Language fa -LegacyInstaller $legacy }
    } finally {
        foreach($file in $savedFiles.Keys) {
            if($null -ne $savedFiles[$file]) {New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null;[IO.File]::WriteAllBytes($file,[byte[]]$savedFiles[$file])}
            elseif(Test-Path -LiteralPath $file) {Remove-Item -LiteralPath $file}
        }
        foreach($path in $backup.Keys) {
            if(!$existed[$path]) {[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($path,$false);continue}
            $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($path)
            foreach($name in $key.GetValueNames()) {if(($path -ne 'Software\Microsoft\Windows\CurrentVersion\Run' -or $name -eq 'RestTimer') -and !$backup[$path].ContainsKey($name)) {$key.DeleteValue($name,$false)}}
            foreach($name in $backup[$path].Keys) {$value=$backup[$path][$name];$key.SetValue($name,$value[0],$value[1])};$key.Dispose()
        }
    }
} finally { Pop-Location }
