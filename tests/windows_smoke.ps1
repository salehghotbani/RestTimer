param(
    [switch]$SkipInstaller,
    [ValidateSet('en', 'fa')][string]$Language = 'en',
    [string]$LegacyInstaller
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    $version = (Get-Content VERSION -Raw).Trim()
    $targetExe = (Resolve-Path dist/Windows/RestTimer.exe).Path
    $installPath = Join-Path $env:TEMP "RestTimer-install-smoke-$PID"
    New-Item -ItemType Directory -Path obj/smoke -Force | Out-Null
    function Invoke-Setup([string]$File, [string]$Log) {
        # Wait for Setup itself: -Wait also waits for the restarted application's process tree.
        $process = Start-Process -FilePath $File -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/LANG=$Language", "/DIR=`"$installPath`"", "/LOG=`"$Log`"") -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(90000)) { throw 'Installer did not finish within 90 seconds.' }
        if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode). See $Log" }
    }
    function Wait-App {
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            $instances = @(Get-Process -Name RestTimer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $targetExe })
            if ($instances.Count -gt 1) { throw 'Setup launched duplicate application instances.' }
            if ($instances.Count -eq 1) {
                try {
                    $mutex = [System.Threading.Mutex]::OpenExisting('Local\RestTimer.Singleton')
                    $mutex.Dispose()
                    return $instances[0]
                } catch [System.Threading.WaitHandleCannotBeOpenedException] { }
            }
            Start-Sleep -Milliseconds 250
        }
        throw 'The installed application did not start.'
    }
    function Test-Upgrade([string]$Label, [bool]$Graceful) {
        $old = Start-Process -FilePath $targetExe -WindowStyle Hidden -PassThru
        $ready = Wait-App
        if ($ready.Id -ne $old.Id) { throw 'Unexpected running application before upgrade.' }
        if (!$old.WaitForInputIdle(10000)) { throw 'Application did not become ready before upgrade.' }
        $log = Join-Path (Get-Location).Path "obj/smoke/upgrade-$Label-$Language.log"
        Invoke-Setup $setup $log
        if (!$old.WaitForExit(10000)) { throw 'Setup did not close the running application.' }
        if ($Graceful -and $old.ExitCode -ne 0) {
            Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1026,1000; StartTime=(Get-Date).AddMinutes(-5)} -ErrorAction SilentlyContinue |
                Format-List TimeCreated,Message | Out-String | Write-Output
            throw "The current version did not exit gracefully. Exit code: $($old.ExitCode)"
        }
        $restarted = Wait-App
        if ($restarted.Id -eq $old.Id) { throw 'Setup did not restart the application.' }
        if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetExe).ProductVersion -notlike "$version*") {
            throw 'Upgrade did not install the new version.'
        }
        $notice = if ($Language -eq 'fa') { 'برنامه باز است.' } else { 'RestTimer is running.' }
        if (!(Select-String -LiteralPath $log -SimpleMatch $notice -Quiet)) { throw 'The translated automatic close/reopen notice was not logged.' }
        $state = Join-Path (Get-Location).Path "obj/smoke/closed-$Label-$Language.ini"
        $helper = Start-Process -FilePath $targetExe -ArgumentList "--close-for-install `"$state`"" -WindowStyle Hidden -PassThru
        if (!$helper.WaitForExit(15000) -or $helper.ExitCode -ne 10 -or !$restarted.WaitForExit(10000)) {
            throw 'Shutdown helper did not close the restarted application.'
        }
        if (!(Select-String -LiteralPath $state -SimpleMatch "Executable=$targetExe" -Quiet)) {
            throw 'Shutdown helper did not save the original executable path.'
        }
        Write-Output "Automatic close and restart passed: $Label, $Language."
    }
    function Test-Uninstall {
        # Exercise enabling startup after installation, when the setup task was unchecked.
        $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        New-Item -Path $runKey -Force | Out-Null
        New-ItemProperty -LiteralPath $runKey -Name RestTimer -Value ('"' + $targetExe + '"') -PropertyType String -Force | Out-Null
        # Inno may replace/rename its uninstaller when upgrading an older setup script.
        $uninstaller = (Get-ItemPropertyValue -LiteralPath $uninstallKey -Name UninstallString).Trim('"')
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $targetExe)) { throw 'Uninstall smoke test failed.' }
        if ((Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).RestTimer) { throw 'Uninstaller left its startup command behind.' }
    }
    if (!$SkipInstaller) {
        $setup = (Resolve-Path "dist/release/RestTimer-v$version-windows-x64-setup.exe").Path
        Invoke-Setup $setup (Join-Path (Get-Location).Path "obj/smoke/install-$Language.log")
        $targetExe = Join-Path $installPath 'RestTimer.exe'
        if (!(Test-Path -LiteralPath $targetExe)) { throw 'Installer did not install the application.' }
        $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{77527277-48F0-4BDF-B74C-99A026A3B8C5}_is1'
        $installedLanguage = Get-ItemPropertyValue -LiteralPath $uninstallKey -Name 'Inno Setup: Language'
        if ($installedLanguage -ne $Language) { throw "Installer did not use the requested language $Language." }
    }
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
            using (var context = (ApplicationContext)Activator.CreateInstance(type, new object[] { false, false }))
            {
                var menu = (ContextMenuStrip)type.GetField("menu", flags).GetValue(context);
                var tray = (NotifyIcon)type.GetField("trayIcon", flags).GetValue(context);
                if (menu.Items[0] != type.GetField("startTimer", flags).GetValue(context)
                    || menu.Items[1] != type.GetField("stopTimer", flags).GetValue(context)
                    || menu.Items[2] != type.GetField("startBreak", flags).GetValue(context))
                    throw new Exception("Start, stop and break must be the first menu entries.");
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
                    ((CheckBox)dialog.GetType().GetField("showTaskbarCountdown", flags).GetValue(dialog)).Checked = false;
                    dialog.GetType().GetMethod("SaveDurations", flags).Invoke(dialog, null);
                    var selected = dialog.GetType().GetProperty("SelectedPreferences").GetValue(dialog);
                    if ((bool)selected.GetType().GetProperty("ShowTaskbarCountdown").GetValue(selected))
                        throw new Exception("Settings did not preserve the disabled taskbar countdown choice.");
                }
            }
            // Application.Run also disposes its context; a surrounding using may dispose it again.
            using (var context = (ApplicationContext)Activator.CreateInstance(type, new object[] { false, false }))
                context.Dispose();
            var badgeType = app.GetType("RestTimer.TaskbarCountdown", true);
            var place = badgeType.GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (float scale in new float[] { 1f, 1.5f, 2f })
            {
                int height = (int)(48 * scale);
                var screen = new Rectangle(-1920, 0, 1920, 1080);
                var bar = new Rectangle(-1920, 1080 - height, 1920, height);
                var trayBounds = new Rectangle(-400, bar.Top, 400, height);
                var strip = new Rectangle(screen.Left, bar.Top - (int)(24 * scale), screen.Width, (int)(24 * scale));
                var position = (Rectangle)place.Invoke(null, new object[] { strip, trayBounds, screen, scale });
                if (position.IsEmpty || position.Right >= trayBounds.Left || !screen.Contains(position) || !strip.Contains(position) || bar.IntersectsWith(position))
                    throw new Exception("Countdown must fit inside its reserved strip, above the taskbar, at all display scales.");
                var hidden = new Rectangle(bar.X, 1078, bar.Width, bar.Height);
                if (!((Rectangle)place.Invoke(null, new object[] { hidden, new Rectangle(-400, hidden.Top, 400, height), screen, scale })).IsEmpty)
                    throw new Exception("An out-of-bounds reservation must not display a countdown.");
            }
            using (var badge = (Form)Activator.CreateInstance(badgeType))
            {
                badge.Size = new Size(100, 20);
                if (badge.ShowInTaskbar || badge.FormBorderStyle != FormBorderStyle.None)
                    throw new Exception("Taskbar countdown must be a fixed borderless tool window.");
                var parameters = (CreateParams)badgeType.GetProperty("CreateParams", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge);
                if ((parameters.ExStyle & 0x08000020) != 0x08000020) throw new Exception("Countdown must not activate or intercept mouse dragging.");
                var updateBadge = badgeType.GetMethod("UpdateDisplay");
                var renderFrame = badgeType.GetMethod("RenderFrame", BindingFlags.Static | BindingFlags.NonPublic);
                var foregroundColor = badgeType.GetMethod("ForegroundColor", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (string phase in new string[] { "work", "rest", "paused" })
                {
                    updateBadge.Invoke(badge, new object[] { "47:12", phase == "rest", phase != "paused", false });
                    if (badge.Visible) throw new Exception("Countdown ignored its disabled setting.");
                    foreach (bool light in new bool[] { true, false })
                    {
                        var color = (Color)foregroundColor.Invoke(null, new object[] { light, false });
                        if ((color.GetBrightness() > 0.5f) == light) throw new Exception("Taskbar text does not contrast with the Windows color mode.");
                        using (var bitmap = (Bitmap)renderFrame.Invoke(null, new object[] { badge.Size, "23:59:59", phase != "work", phase != "paused", color }))
                        {
                            int transparent = 0, left = 0, right = 0;
                            for (int y = 0; y < bitmap.Height; y++)
                                for (int x = 0; x < bitmap.Width; x++)
                                {
                                    if (bitmap.GetPixel(x, y).A == 0) transparent++;
                                    else if (x < 55) left++; else right++;
                                }
                            if (transparent < bitmap.Width * bitmap.Height * 0.6 || bitmap.GetPixel(0, 0).A != 0 || left == 0 || right == 0)
                                throw new Exception("Taskbar text must have a transparent background with a side-by-side label and time.");
                            bitmap.Save("obj/smoke/windows-taskbar-" + (light ? "light-" : "dark-") + phase + ".png", ImageFormat.Png);
                            using (var preview = new Bitmap(140, 36))
                            using (var graphics = Graphics.FromImage(preview))
                            {
                                graphics.Clear(light ? Color.FromArgb(245, 239, 232) : Color.FromArgb(30, 30, 30));
                                graphics.DrawImageUnscaled(bitmap, 20, 8);
                                preview.Save("obj/smoke/windows-taskbar-preview-" + (light ? "light-" : "dark-") + phase + ".png", ImageFormat.Png);
                            }
                        }
                    }
                }
                var reservationType = app.GetType("RestTimer.AppbarReservation", true);
                var workArea = reservationType.GetMethod("WorkArea", BindingFlags.Static | BindingFlags.NonPublic);
                var primary = Screen.PrimaryScreen.Bounds;
                var before = (Rectangle)workArea.Invoke(null, new object[] { primary });
                updateBadge.Invoke(badge, new object[] { "47:12", false, true, true });
                Application.DoEvents();
                Console.WriteLine("Live taskbar anchor: visible=" + badge.Visible + ", bounds=" + badge.Bounds);
                bool reserved = (bool)badgeType.GetProperty("HasReservation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge);
                var reservedBounds = (Rectangle)badgeType.GetProperty("ReservedBounds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge);
                if (reserved)
                {
                    var during = (Rectangle)workArea.Invoke(null, new object[] { primary });
                    if (during.Bottom > before.Bottom - reservedBounds.Height || !reservedBounds.Contains(badge.Bounds))
                        throw new Exception("The Shell did not reserve real workspace for the countdown.");
                    Console.WriteLine("Reserved work area: before=" + before + ", during=" + during + ", strip=" + reservedBounds);
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        badgeType.GetField("nextPosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(badge, DateTime.MinValue);
                        updateBadge.Invoke(badge, new object[] { "47:11", attempt == 1, attempt != 2, true });
                        Application.DoEvents();
                        if ((Rectangle)workArea.Invoke(null, new object[] { primary }) != during)
                            throw new Exception("Refreshing the countdown accumulated additional workspace reservations.");
                    }
                }
                if (badge.Visible && !reserved) throw new Exception("A countdown must never overlay the taskbar without a reservation.");
                if (badge.Visible && !(bool)badgeType.GetProperty("FrameUploaded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge))
                    throw new Exception("The per-pixel transparent taskbar frame was not uploaded to Windows.");
                updateBadge.Invoke(badge, new object[] { "47:12", false, true, false });
                Application.DoEvents();
                if ((bool)badgeType.GetProperty("HasReservation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge))
                    throw new Exception("Disabling the countdown did not unregister its appbar.");
                if (reserved && (Rectangle)workArea.Invoke(null, new object[] { primary }) != before)
                    throw new Exception("Disabling the countdown did not restore the previous work area.");
                if (reserved)
                {
                    updateBadge.Invoke(badge, new object[] { "15:00", true, true, true });
                    Application.DoEvents();
                    if (!(bool)badgeType.GetProperty("HasReservation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge))
                        throw new Exception("Re-enabling the countdown failed to reserve workspace.");
                    badge.Dispose();
                    Application.DoEvents();
                    if ((Rectangle)workArea.Invoke(null, new object[] { primary }) != before)
                        throw new Exception("Closing the countdown did not restore the previous work area.");
                    Console.WriteLine("Repeated refresh, disable/re-enable and disposal restored the original workspace.");
                }
            }
            var updaterType = app.GetType("RestTimer.UpdateService", true);
            var updateService = Activator.CreateInstance(updaterType, new object[] { System.IO.Path.GetFullPath("obj/smoke/update-ui") });
            var updateState = updaterType.GetProperty("State").GetValue(updateService);
            var release = app.GetType("RestTimer.UpdateClient", true).GetMethod("ParseRelease", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "{\"tag_name\":\"v9.0.0\",\"body\":\"RestTimer 9.0.0\\n\\n## تغییرات نسخهٔ جدید\\n- دکمه‌های شروع و توقف بالای منو قرار دارند.\\n- نمایش به‌روزرسانی با حالت روشن و تاریک ویندوز هماهنگ است.\\n- انتخاب نسخه و خواندن تغییرات ساده‌تر شده است.\\n\\n## Update experience\\nRead the release notes, approve the update and let RestTimer download and install it automatically. Your timer settings remain available after restarting.\\n\\n### نکتهٔ نصب\\nبرنامه هنگام نصب بسته و پس از پایان دوباره اجرا می‌شود.\",\"assets\":[]}" });
            updateState.GetType().GetProperty("Latest").SetValue(updateState, release);
            using (var dialog = (Form)Activator.CreateInstance(app.GetType("RestTimer.UpdateDialog", true),
                SystemIcons.Application, updateService, false, false))
            {
                dialog.Show();
                Application.DoEvents();
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var notes = (RichTextBox)dialog.GetType().GetField("latestNotes", fields).GetValue(dialog);
                var historyPanel = (Panel)dialog.GetType().GetField("historyPanel", fields).GetValue(dialog);
                var install = (Button)dialog.GetType().GetField("install", fields).GetValue(dialog);
                var versions = (ListBox)dialog.GetType().GetField("versions", fields).GetValue(dialog);
                versions.Items.Add(release);
                versions.SelectedIndex = 1;
                if (!notes.Text.Contains("تغییرات") || notes.Text.Contains("RestTimer 9.0.0") || !install.Enabled)
                    throw new Exception("Update consent or history UI missing.");
                var themeType = app.GetType("RestTimer.Theme", true);
                var mode = themeType.GetProperty("LightOverride", BindingFlags.Static | BindingFlags.NonPublic);
                try
                {
                    foreach (bool light in new bool[] { false, true })
                    {
                        mode.SetValue(null, light);
                        themeType.GetMethod("Refresh").Invoke(null, null);
                        dialog.GetType().GetMethod("ApplyTheme", fields).Invoke(dialog, null);
                        if ((notes.BackColor.GetBrightness() > 0.5f) != light || (notes.ForeColor.GetBrightness() > 0.5f) == light)
                            throw new Exception("Update notes did not adopt the selected OS color mode.");
                        foreach (bool historyPage in new bool[] { false, true })
                        {
                            ((Button)dialog.GetType().GetField(historyPage ? "historyTab" : "latestTab", fields).GetValue(dialog)).PerformClick();
                            Application.DoEvents();
                            if (historyPanel.Visible != historyPage) throw new Exception("Page navigation failed.");
                            using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                            {
                                dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                                bitmap.Save("obj/smoke/windows-" + (historyPage ? "history-" : "updates-") + (light ? "light" : "dark") + ".png", ImageFormat.Png);
                            }
                        }
                    }
                    notes.Select(2, 3);
                    mode.SetValue(null, false);
                    Thread.Sleep(800);
                    Application.DoEvents();
                    if (notes.BackColor.GetBrightness() > 0.5f || notes.SelectionStart != 2 || notes.SelectionLength != 3)
                        throw new Exception("Live theme switching lost the notes selection or failed to recolor the page.");
                    dialog.Size = new Size(650, 550);
                    Application.DoEvents();
                    var viewport = dialog.RectangleToScreen(dialog.ClientRectangle);
                    var check = (Button)dialog.GetType().GetField("check", fields).GetValue(dialog);
                    foreach (Control button in new Control[] { install, check, (Control)dialog.CancelButton })
                        if (!viewport.Contains(button.RectangleToScreen(button.ClientRectangle)))
                            throw new Exception("An update action was clipped in the compact window.");
                    var highContrast = themeType.GetField("highContrast", BindingFlags.Static | BindingFlags.NonPublic);
                    var originalContrast = highContrast.GetValue(null);
                    highContrast.SetValue(null, true);
                    if ((Color)themeType.GetProperty("OnSelection").GetValue(null) != SystemColors.HighlightText)
                        throw new Exception("High contrast selection text must use the system highlight foreground.");
                    highContrast.SetValue(null, originalContrast);
                }
                finally { mode.SetValue(null, null); themeType.GetMethod("Refresh").Invoke(null, null); }
            }
            Console.WriteLine("Windows tray, settings, overlay, update consent and offline history passed.");
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
        Test-Upgrade 'current' $true
        Test-Uninstall
        if ($LegacyInstaller) {
            Invoke-Setup (Resolve-Path $LegacyInstaller).Path (Join-Path (Get-Location).Path "obj/smoke/legacy-install-$Language.log")
            if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetExe).ProductVersion -notlike '1.3.1*') { throw 'Expected the actual v1.3.1 application for the legacy upgrade test.' }
            Test-Upgrade 'v1.3.1' $false
            Test-Uninstall
        }
        Write-Output "Installer installation and removal passed in language $Language."
    }
} finally {
    Pop-Location
}
