$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    if(git status --porcelain) { throw 'Commit changes before checking the exact tree to be pushed.' }
    $tree=git rev-parse 'HEAD^{tree}'
    $receipt='build/local-checks-pass.json'
    if(Test-Path $receipt) {
        $passed=Get-Content $receipt -Raw | ConvertFrom-Json
        if($passed.tree -eq $tree -and (Get-Date).ToUniversalTime() - [datetime]$passed.time -lt [timespan]::FromHours(24)) { Write-Output 'This exact tree has passed all local checks.'; return }
    }
    function Run([scriptblock]$Command) { & $Command; if($LASTEXITCODE -ne 0) {throw 'Local check failed; push is blocked.'} }
    if(Get-Process RestTimer -ErrorAction SilentlyContinue) {throw 'Exit RestTimer before isolated installation/upgrade checks; push is blocked.'}
    New-Item -ItemType Directory -Force build | Out-Null
    $processes=@()
    foreach($platform in @('windows','linux')) {
        $script=Join-Path (Get-Location) "tools/check-$platform.ps1"
        $processes+=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile','-File',"`"$script`"") -WindowStyle Hidden -PassThru -RedirectStandardOutput "build/check-$platform.log" -RedirectStandardError "build/check-$platform-errors.log"
    }
    foreach($process in $processes) { $process.WaitForExit() }
    if($processes | Where-Object ExitCode -ne 0) {throw 'Local checks failed; see build/check-*.log. Push is blocked.'}
    if((git rev-parse 'HEAD^{tree}') -ne $tree -or (git status --porcelain)) {throw 'Source changed during checks; push is blocked.'}
    @{tree=$tree;time=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content $receipt
    Write-Output 'All Windows/Linux, installer, X11 and Wayland checks passed locally.'
} finally { Pop-Location }
