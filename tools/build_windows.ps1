$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build -c Release -o dist/Windows
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
    $compiler = $env:ISCC
    if (!$compiler) { $compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
    if (!(Test-Path -LiteralPath $compiler)) { throw 'Install Inno Setup 6, or set ISCC to ISCC.exe.' }
    $version = (Get-Content VERSION -Raw).Trim()
    & $compiler "/DAppVersion=$version" packaging/installer.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} finally {
    Pop-Location
}
