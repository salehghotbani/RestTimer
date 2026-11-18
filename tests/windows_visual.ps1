param([string[]]$Languages)
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    New-Item -ItemType Directory -Force obj/visual-audit | Out-Null
    $compiler="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $source=(Resolve-Path tests/VisualAudit.cs).Path
    $target=Join-Path (Get-Location) 'obj/visual-audit/VisualAudit.exe'
    & $compiler /nologo /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$target" $source
    if($LASTEXITCODE -ne 0) {throw 'Visual audit compilation failed.'}
    Copy-Item dist/Windows/RestTimer.exe.config obj/visual-audit/VisualAudit.exe.config
    & $target @Languages
    if($LASTEXITCODE -ne 0) {throw 'Visual audit failed.'}
} finally {Pop-Location}
