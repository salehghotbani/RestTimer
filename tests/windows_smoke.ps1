param([switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    $version = (Get-Content VERSION -Raw).Trim()
    $targetExe = (Resolve-Path dist/Windows/RestTimer.exe).Path
    $installPath = Join-Path $env:TEMP "RestTimer-install-smoke-$PID"
    if (!$SkipInstaller) {
        $setup = (Resolve-Path "dist/release/RestTimer-v$version-windows-x64-setup.exe").Path
        $install = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/DIR=`"$installPath`"") -WindowStyle Hidden -Wait -PassThru
        if ($install.ExitCode -ne 0) { throw "Installer failed: $($install.ExitCode)" }
        $targetExe = Join-Path $installPath 'RestTimer.exe'
        if (!(Test-Path -LiteralPath $targetExe)) { throw 'Installer did not install the application.' }
    }
    New-Item -ItemType Directory -Path obj/smoke -Force | Out-Null
    @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
internal static class Smoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = app.GetType("RestTimer.RestTimerContext", true);
            using (var context = (ApplicationContext)Activator.CreateInstance(type))
            {
                var menu = (ContextMenuStrip)type.GetField("menu", flags).GetValue(context);
                var tray = (NotifyIcon)type.GetField("trayIcon", flags).GetValue(context);
                if (tray.ContextMenuStrip != menu || !menu.AutoClose) throw new Exception("Native right-click menu or auto-close missing.");
                type.GetMethod("TrayMouseUp", flags).Invoke(context, new object[] { null, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                Application.DoEvents();
                if (!menu.Visible || menu.Renderer.GetType().Name != "TrayMenuRenderer") throw new Exception("Styled left-click menu did not open.");
                using (var bitmap = new Bitmap(menu.Width, menu.Height))
                {
                    menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
                    bitmap.Save("obj/smoke/windows-menu.png", ImageFormat.Png);
                }
                menu.Close();
                ((ToolStripMenuItem)type.GetField("startBreak", flags).GetValue(context)).PerformClick();
                type.GetMethod("UpdateTimer", flags).Invoke(context, null);
                if (Application.OpenForms.Count != Screen.AllScreens.Length) throw new Exception("Display coverage failed.");
                var overlay = Application.OpenForms[0];
                ((Button)overlay.GetType().GetProperty("Skip").GetValue(overlay)).PerformClick();
                if (Application.OpenForms.Count != 0) throw new Exception("Skip failed.");
            }
            Console.WriteLine("Windows tray menus, display coverage and skip passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
'@ | Set-Content -LiteralPath obj/smoke/Smoke.cs -Encoding utf8
    $smokeSource = (Resolve-Path 'obj\smoke\Smoke.cs').Path
    $smokeExe = Join-Path (Get-Location).Path 'obj\smoke\Smoke.exe'
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$smokeExe" $smokeSource
    if ($LASTEXITCODE -ne 0) { throw 'Smoke test compilation failed.' }
    Copy-Item -LiteralPath "$targetExe.config" -Destination obj/smoke/Smoke.exe.config
    & ./obj/smoke/Smoke.exe $targetExe
    if ($LASTEXITCODE -ne 0) { throw 'Windows smoke tests failed.' }
    if (!$SkipInstaller) {
        # Exercise enabling startup after installation, when the setup task was unchecked.
        $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        New-ItemProperty -LiteralPath $runKey -Name RestTimer -Value ('"' + $targetExe + '"') -PropertyType String -Force | Out-Null
        $uninstall = Start-Process -FilePath (Join-Path $installPath 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $targetExe)) { throw 'Uninstall smoke test failed.' }
        if (Get-ItemPropertyValue -LiteralPath $runKey -Name RestTimer -ErrorAction SilentlyContinue) { throw 'Uninstaller left its startup command behind.' }
        Write-Output 'Installer installation and removal passed.'
    }
} finally {
    Pop-Location
}
