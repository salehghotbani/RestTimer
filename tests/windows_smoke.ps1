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
using System.Threading;
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
                var preferencesType = app.GetType("RestTimer.Preferences", true);
                type.GetField("preferences", flags).SetValue(context, Activator.CreateInstance(preferencesType, 2, 2));
                var clock = (System.Diagnostics.Stopwatch)type.GetField("clock", flags).GetValue(context);
                var update = type.GetMethod("UpdateTimer", flags);
                var startTimer = (ToolStripMenuItem)type.GetField("startTimer", flags).GetValue(context);
                var stopTimer = (ToolStripMenuItem)type.GetField("stopTimer", flags).GetValue(context);
                clock.Restart();
                Thread.Sleep(100);
                stopTimer.PerformClick();
                var frozen = clock.Elapsed;
                Thread.Sleep(2200);
                Application.DoEvents();
                update.Invoke(context, null);
                if (clock.Elapsed != frozen || Application.OpenForms.Count != 0 || !startTimer.Enabled)
                    throw new Exception("Work did not remain paused past its original deadline.");
                startTimer.PerformClick();
                if (!clock.IsRunning || clock.Elapsed < frozen || startTimer.Enabled)
                    throw new Exception("Start reset the work timer instead of resuming.");
                stopTimer.PerformClick();
                ((ToolStripMenuItem)type.GetField("startBreak", flags).GetValue(context)).PerformClick();
                update.Invoke(context, null);
                if (!clock.IsRunning) throw new Exception("Explicitly starting a break must start its timer.");
                if (Application.OpenForms.Count != Screen.AllScreens.Length) throw new Exception("Display coverage failed.");
                var overlay = Application.OpenForms[0];
                ((Button)overlay.GetType().GetProperty("Stop").GetValue(overlay)).PerformClick();
                frozen = clock.Elapsed;
                Thread.Sleep(2200);
                Application.DoEvents();
                update.Invoke(context, null);
                if (clock.Elapsed != frozen || Application.OpenForms.Count != Screen.AllScreens.Length)
                    throw new Exception("The break expired while stopped.");
                type.GetMethod("CloseOverlays", flags).Invoke(context, null);
                update.Invoke(context, null);
                foreach (Form display in Application.OpenForms)
                {
                    if (!((Button)display.GetType().GetProperty("Start").GetValue(display)).Enabled
                        || ((Button)display.GetType().GetProperty("Stop").GetValue(display)).Enabled)
                        throw new Exception("Paused controls were not restored on a recreated display.");
                }
                overlay = Application.OpenForms[0];
                var card = (Control)overlay.GetType().GetField("card", flags).GetValue(overlay);
                using (var bitmap = new Bitmap(card.Width, card.Height))
                {
                    card.DrawToBitmap(bitmap, new Rectangle(Point.Empty, card.Size));
                    bitmap.Save("obj/smoke/windows-overlay-paused.png", ImageFormat.Png);
                }
                ((Button)overlay.GetType().GetProperty("Start").GetValue(overlay)).PerformClick();
                if (!clock.IsRunning || clock.Elapsed < frozen)
                    throw new Exception("Overlay start did not resume the shared break timer.");
                foreach (Form display in Application.OpenForms)
                    if (((Button)display.GetType().GetProperty("Start").GetValue(display)).Enabled)
                        throw new Exception("Displays disagree about running state.");
                stopTimer.PerformClick();
                ((Button)overlay.GetType().GetProperty("Skip").GetValue(overlay)).PerformClick();
                if (Application.OpenForms.Count != 0) throw new Exception("Skip failed.");
                if (clock.IsRunning || clock.Elapsed != TimeSpan.Zero)
                    throw new Exception("Skip unexpectedly resumed a stopped timer.");
                var start = (Action)Delegate.CreateDelegate(typeof(Action), context, type.GetMethod("StartTimer", flags));
                var stop = (Action)Delegate.CreateDelegate(typeof(Action), context, type.GetMethod("StopTimer", flags));
                using (var dialog = (Form)Activator.CreateInstance(app.GetType("RestTimer.SettingsDialog", true),
                    Activator.CreateInstance(preferencesType, 2, 2), SystemIcons.Application, start, stop))
                {
                    type.GetField("settingsDialog", flags).SetValue(context, dialog);
                    type.GetField("editingSettings", flags).SetValue(context, true);
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-10000, -10000);
                    dialog.Show();
                    update.Invoke(context, null);
                    ((Button)dialog.GetType().GetField("startTimer", flags).GetValue(dialog)).PerformClick();
                    if (!clock.IsRunning) throw new Exception("Settings start failed.");
                    ((Button)dialog.GetType().GetField("stopTimer", flags).GetValue(dialog)).PerformClick();
                    if (clock.IsRunning) throw new Exception("Settings stop failed.");
                    type.GetMethod("ResetClock", flags).Invoke(context, null);
                    if (clock.IsRunning) throw new Exception("Changing durations must preserve a stopped timer.");
                    using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                        bitmap.Save("obj/smoke/windows-settings.png", ImageFormat.Png);
                    }
                    type.GetField("settingsDialog", flags).SetValue(context, null);
                    type.GetField("editingSettings", flags).SetValue(context, false);
                }
            }
            Console.WriteLine("Windows tray, settings and overlay start/stop, exact resume, display coverage and skip passed.");
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
        New-Item -Path $runKey -Force | Out-Null
        New-ItemProperty -LiteralPath $runKey -Name RestTimer -Value ('"' + $targetExe + '"') -PropertyType String -Force | Out-Null
        $uninstall = Start-Process -FilePath (Join-Path $installPath 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $targetExe)) { throw 'Uninstall smoke test failed.' }
        if ((Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).RestTimer) { throw 'Uninstaller left its startup command behind.' }
        Write-Output 'Installer installation and removal passed.'
    }
} finally {
    Pop-Location
}
