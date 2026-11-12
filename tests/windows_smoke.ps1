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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
internal static class Smoke
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    {
        foreach(Control child in parent.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; }
    }
    private static int CloseChoosing(Form dialog, DialogResult choice)
    {
        int prompts=0;
        using(var timer=new System.Windows.Forms.Timer{Interval=20})
        {
            timer.Tick+=delegate {
                var prompt=Application.OpenForms.Cast<Form>().FirstOrDefault(form=>form.Name=="UnsavedChangesDialog");
                if(prompt==null) return;
                timer.Stop();prompts++;
                if(choice==DialogResult.Cancel) prompt.Close();
                else Descendants(prompt).OfType<Button>().Single(button=>button.DialogResult==choice).PerformClick();
            };
            timer.Start();dialog.Close();
        }
        return prompts;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint relation);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    private static Rectangle WorkArea(Rectangle screen)
    {
        var rect = new NativeRect { Left=screen.Left, Top=screen.Top, Right=screen.Right, Bottom=screen.Bottom };
        var info = new MonitorInfo { Size=(uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MonitorInfo)) };
        if(!GetMonitorInfo(MonitorFromRect(ref rect,2),ref info)) throw new Exception("Could not read native work area.");
        return Rectangle.FromLTRB(info.Work.Left,info.Work.Top,info.Work.Right,info.Work.Bottom);
    }

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
                if (menu.Items.OfType<ToolStripMenuItem>().Count() != 8) throw new Exception("Release history must only appear in the settings sidebar.");
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
                    var durationEditor = dialog.GetType().GetField("workDuration", flags).GetValue(dialog);
                    foreach (string partName in new[] { "minutes", "seconds" })
                    {
                        var part = (TextBox)durationEditor.GetType().GetField(partName, flags).GetValue(durationEditor);
                        string originalPart = part.Text;
                        foreach (string valid in new[] { "0", "59", "۵۹", "٥٩" })
                        {
                            part.Text = valid;
                            if (part.Text != valid) throw new Exception("Valid localized duration digits were rejected.");
                            foreach (string invalid in new[] { "60", "99", "۶۰", "٦٠", "-1", "x" })
                            {
                                part.Text = invalid;
                                if (part.Text != valid) throw new Exception("Minutes or seconds allowed an invalid value while editing.");
                            }
                        }
                        part.Text = originalPart;
                    }
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
            var measure = badgeType.GetMethod("MeasureFrame", BindingFlags.Static | BindingFlags.NonPublic);
            var coversDisplay = badgeType.GetMethod("CoversDisplay", BindingFlags.Static | BindingFlags.NonPublic);
            var fullscreenMonitor = new Rectangle(-1920, 0, 1920, 1080);
            if (!(bool)coversDisplay.Invoke(null, new object[] { fullscreenMonitor, fullscreenMonitor })
                || (bool)coversDisplay.Invoke(null, new object[] { new Rectangle(0, 0, 1920, 1080), fullscreenMonitor })
                || (bool)coversDisplay.Invoke(null, new object[] { new Rectangle(-1920, 0, 1920, 1040), fullscreenMonitor }))
                throw new Exception("Fullscreen detection confused work area, monitor bounds or secondary displays.");
            foreach (float scale in new float[] { 1f, 1.25f, 1.5f, 2f })
            foreach (var screen in new[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            foreach (string edge in new[] { "bottom", "left", "right", "top" })
            {
                int height = (int)(48 * scale), width = (int)(80 * scale);
                bool vertical = edge == "left" || edge == "right";
                var bar = vertical
                    ? new Rectangle(edge == "left" ? screen.Left : screen.Right - width, screen.Top, width, screen.Height)
                    : new Rectangle(screen.Left, edge == "top" ? screen.Top : screen.Bottom - height, screen.Width, height);
                var trayBounds = vertical ? new Rectangle(bar.Left, bar.Bottom - 200, width, 200)
                    : new Rectangle(screen.Right - 400, bar.Top, 400, height);
                var size = (Size)measure.Invoke(null, new object[] { "23:59:59", true, scale });
                var position = (Rectangle)place.Invoke(null, new object[] { bar, trayBounds, screen, size, scale });
                if (position.IsEmpty || !screen.Contains(position) || bar.IntersectsWith(position) || position.Size != size)
                    throw new Exception("Floating countdown placement failed for " + edge + " taskbar at scale " + scale + ".");
                if (vertical && (edge == "left" ? position.Left <= bar.Right || position.Left > bar.Right + 10 * scale
                    : position.Right >= bar.Left || position.Right < bar.Left - 10 * scale))
                    throw new Exception("A vertical taskbar's countdown must stay beside its inner edge.");
                if (!vertical && (position.Right >= trayBounds.Left || (edge == "top" ? position.Top <= bar.Bottom : position.Bottom >= bar.Top)))
                    throw new Exception("The floating box must sit outside the taskbar, near the notification area.");
                var hidden = vertical
                    ? new Rectangle(edge == "left" ? screen.Left - width + 2 : screen.Right - 2, screen.Top, width, screen.Height)
                    : new Rectangle(screen.Left, edge == "top" ? screen.Top - height + 2 : screen.Bottom - 2, screen.Width, height);
                var hiddenPosition = (Rectangle)place.Invoke(null, new object[] { hidden, Rectangle.Empty, screen, size, scale });
                if (hiddenPosition.IsEmpty || !screen.Contains(hiddenPosition) || hidden.IntersectsWith(hiddenPosition))
                    throw new Exception("The floating countdown must remain visible with an auto-hidden taskbar.");
                if (!((Rectangle)place.Invoke(null, new object[] { bar, trayBounds, new Rectangle(0, 0, 20, 20), size, scale })).IsEmpty)
                    throw new Exception("An oversized floating box must not be placed outside the screen.");
            }
            using (var badge = (Form)Activator.CreateInstance(badgeType))
            {
                if (badge.ShowInTaskbar || badge.FormBorderStyle != FormBorderStyle.None || !badge.TopMost)
                    throw new Exception("The countdown must be a topmost borderless tool window.");
                var parameters = (CreateParams)badgeType.GetProperty("CreateParams", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge);
                if ((parameters.ExStyle & 0x08000020) != 0x08000020) throw new Exception("Countdown must not activate or intercept mouse dragging.");
                var updateBadge = badgeType.GetMethod("UpdateDisplay");
                var renderFrame = badgeType.GetMethod("RenderFrame", BindingFlags.Static | BindingFlags.NonPublic);
                var foregroundColor = badgeType.GetMethod("ForegroundColor", BindingFlags.Static | BindingFlags.NonPublic);
                var backgroundColor = badgeType.GetMethod("BackgroundColor", BindingFlags.Static | BindingFlags.NonPublic);
                var i18n = app.GetType("RestTimer.I18n", true);
                string originalLanguage = (string)i18n.GetProperty("Code").GetValue(null);
                foreach (string code in (string[])i18n.GetField("SupportedCodes").GetValue(null))
                {
                    i18n.GetMethod("Use").Invoke(null, new object[] { code });
                    foreach (string phase in new[] { "work", "rest", "paused" })
                    foreach (bool light in new[] { true, false })
                    {
                        var size = (Size)measure.Invoke(null, new object[] { "23:59:59", phase == "rest", 1f });
                        var stable = (Size)measure.Invoke(null, new object[] { "00:00:00", phase == "rest", 1f });
                        if (size != stable || size.Width > 240 || size.Height != 30)
                            throw new Exception("The floating box must fit its localized text without countdown-width jitter.");
                        var foreground = (Color)foregroundColor.Invoke(null, new object[] { light, false });
                        var background = (Color)backgroundColor.Invoke(null, new object[] { light, false });
                        if ((foreground.GetBrightness() > 0.5f) == light || (background.GetBrightness() > 0.5f) != light)
                            throw new Exception("Floating box colors must follow the system mode.");
                        using (var bitmap = (Bitmap)renderFrame.Invoke(null, new object[] { size, "23:59:59", phase == "rest", phase != "paused", light, false }))
                        {
                            int opaque = 0;
                            for (int y = 0; y < bitmap.Height; y++)
                                for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A == 255) opaque++;
                            if (opaque < bitmap.Width * bitmap.Height * 0.6 || bitmap.GetPixel(0, 0).A != 0
                                || bitmap.GetPixel(bitmap.Width / 2, 5).ToArgb() != background.ToArgb())
                                throw new Exception("Floating timer needs a solid background, rounded transparent corners and compact bounds.");
                            bitmap.Save("obj/smoke/windows-taskbar-" + code + "-" + (light ? "light-" : "dark-") + phase + ".png", ImageFormat.Png);
                            if (code == "en" || code == "fa")
                                using (var preview = new Bitmap((size.Width + 24) * 3, (size.Height + 20) * 3))
                                using (var graphics = Graphics.FromImage(preview))
                                {
                                    graphics.Clear(light ? Color.FromArgb(224, 228, 234) : Color.FromArgb(12, 18, 27));
                                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                                    graphics.DrawImage(bitmap, new Rectangle(36, 30, size.Width * 3, size.Height * 3));
                                    preview.Save("obj/smoke/windows-taskbar-preview-" + code + "-" + (light ? "light-" : "dark-") + phase + ".png", ImageFormat.Png);
                                }
                        }
                    }
                }
                i18n.GetMethod("Use").Invoke(null, new object[] { originalLanguage });
                if ((Color)foregroundColor.Invoke(null, new object[] { false, true }) != SystemColors.WindowText
                    || (Color)backgroundColor.Invoke(null, new object[] { false, true }) != SystemColors.Window)
                    throw new Exception("High-contrast floating colors must use the system window palette.");
                var primary = Screen.PrimaryScreen.Bounds;
                var before = WorkArea(primary);
                updateBadge.Invoke(badge, new object[] { "47:12", false, true, true });
                Application.DoEvents();
                Console.WriteLine("Live floating countdown: visible=" + badge.Visible + ", bounds=" + badge.Bounds);
                if (WorkArea(primary) != before) throw new Exception("The floating timer must never reserve a desktop row.");
                if (badge.Visible && (!(bool)badgeType.GetProperty("FrameUploaded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(badge)
                    || (GetWindowLong(badge.Handle, -20) & 8) == 0))
                    throw new Exception("The opaque floating frame must be uploaded and remain native topmost.");
                if (badge.Visible)
                {
                    using (var cover = new Form { FormBorderStyle=FormBorderStyle.None, StartPosition=FormStartPosition.Manual,
                        Bounds=primary, ShowInTaskbar=false, BackColor=Color.DimGray })
                    {
                        cover.Show(); cover.Activate(); SetForegroundWindow(cover.Handle); Application.DoEvents();
                        cover.Bounds = primary;
                        Application.DoEvents();
                        var foreground = GetForegroundWindow();
                        updateBadge.Invoke(badge, new object[] { "47:11", false, true, true });
                        Application.DoEvents();
                        var isFullscreen = badgeType.GetMethod("IsFullscreenWindow", BindingFlags.Static | BindingFlags.NonPublic);
                        if (!(bool)isFullscreen.Invoke(null, new object[]{cover.Handle, primary})
                            || foreground == cover.Handle && badge.Visible || GetForegroundWindow()!=foreground || WorkArea(primary)!=before)
                            throw new Exception("The floating timer must hide over a foreground fullscreen window without taking focus or workspace changes. Visible=" + badge.Visible + ", foreground=" + foreground + ", cover=" + cover.Handle + ", bounds=" + cover.Bounds);
                        cover.Bounds = Rectangle.Inflate(primary, -160, -120);
                        Application.DoEvents();
                        var restoredForeground = GetForegroundWindow();
                        updateBadge.Invoke(badge, new object[] { "47:10", false, true, true });
                        Application.DoEvents();
                        if ((bool)isFullscreen.Invoke(null, new object[]{cover.Handle, primary})
                            || !badge.Visible || GetForegroundWindow()!=restoredForeground || WorkArea(primary)!=before)
                            throw new Exception("The timer did not return after leaving fullscreen, or it took focus. Visible=" + badge.Visible
                                + ", foreground before=" + restoredForeground + ", after=" + GetForegroundWindow() + ", cover=" + cover.Handle + ", badge=" + badge.Handle + ", cover bounds=" + cover.Bounds
                                + ", fullscreen=" + isFullscreen.Invoke(null,new object[]{cover.Handle,primary}));
                        Console.WriteLine(foreground == cover.Handle ? "Fullscreen suppression and automatic restoration passed without taking focus."
                            : "Native fullscreen geometry passed; desktop foreground lock prevented activation of the simulated game.");
                    }
                }
                updateBadge.Invoke(badge, new object[] { "15:00", true, false, true });
                Application.DoEvents();
                if (WorkArea(primary) != before) throw new Exception("Changing timer phase or pause state changed the desktop workspace.");
                updateBadge.Invoke(badge, new object[] { "15:00", true, false, false });
                if (badge.Visible || WorkArea(primary) != before) throw new Exception("Disabling the floating box changed the desktop workspace.");
                updateBadge.Invoke(badge, new object[] { "15:00", true, false, true });
                badge.Dispose(); Application.DoEvents();
                if (WorkArea(primary) != before) throw new Exception("Closing the floating box changed the desktop workspace.");
                Console.WriteLine("Topmost floating box, localized backgrounds and unchanged desktop workspace passed.");
            }
            var updaterType = app.GetType("RestTimer.UpdateService", true);
            var updateService = Activator.CreateInstance(updaterType, new object[] { System.IO.Path.GetFullPath("obj/smoke/update-ui") });
            var updateState = updaterType.GetProperty("State").GetValue(updateService);
            var release = app.GetType("RestTimer.UpdateClient", true).GetMethod("ParseRelease", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "{\"tag_name\":\"v9.0.0\",\"body\":\"RestTimer 9.0.0\\n\\n## تغییرات نسخهٔ جدید\\n- دکمه‌های شروع و توقف بالای منو قرار دارند.\\n- نمایش به‌روزرسانی با حالت روشن و تاریک ویندوز هماهنگ است.\\n- انتخاب نسخه و خواندن تغییرات ساده‌تر شده است.\\n\\n## Update experience\\nRead the release notes, approve the update and let RestTimer download and install it automatically. Your timer settings remain available after restarting.\\n\\n### نکتهٔ نصب\\nبرنامه هنگام نصب بسته و پس از پایان دوباره اجرا می‌شود.\",\"assets\":[]}" });
            updateState.GetType().GetProperty("Latest").SetValue(updateState, release);
            using (var dialog = (Form)Activator.CreateInstance(app.GetType("RestTimer.SettingsDialog", true),
                Activator.CreateInstance(app.GetType("RestTimer.Preferences", true), 3600, 900), SystemIcons.Application,
                (Action)(() => {}), (Action)(() => {}), updateService, "updates", false))
            {
                dialog.Show();
                Application.DoEvents();
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var panel = (Control)dialog.GetType().GetField("updatePanel", fields).GetValue(dialog);
                var panelType = panel.GetType();
                var notes = (RichTextBox)panelType.GetField("latestNotes", fields).GetValue(panel);
                var historyPanel = (Panel)panelType.GetField("historyPanel", fields).GetValue(panel);
                var install = (Button)panelType.GetField("install", fields).GetValue(panel);
                var versions = (ListBox)panelType.GetField("versions", fields).GetValue(panel);
                var generalDaily = (CheckBox)dialog.GetType().GetField("daily", fields).GetValue(dialog);
                var updateDaily = (CheckBox)panelType.GetField("daily", fields).GetValue(panel);
                bool savedDaily=(bool)updateState.GetType().GetProperty("DailyEnabled").GetValue(updateState);
                generalDaily.Checked = !savedDaily;
                var restored = Activator.CreateInstance(updaterType, new object[] { System.IO.Path.GetFullPath("obj/smoke/update-ui") });
                var restoredState = updaterType.GetProperty("State").GetValue(restored);
                if (updateDaily.Checked != !savedDaily || (bool)restoredState.GetType().GetProperty("DailyEnabled").GetValue(restoredState)!=savedDaily)
                    throw new Exception("Daily-check drafts must synchronize without persisting before Save.");
                updateDaily.Checked = savedDaily;
                if (generalDaily.Checked!=savedDaily) throw new Exception("Update daily-check preference did not sync back to General.");
                if(dialog.CancelButton!=null || Descendants(dialog).OfType<Button>().Any(button=>button.Text=="Close / cancel" || button.Text=="Cancel"))
                    throw new Exception("Removed close/cancel buttons remain in the settings panel.");
                versions.Items.Add(release);
                versions.SelectedIndex = 1;
                if (!notes.Text.Contains("تغییرات") || notes.Text.Contains("RestTimer 9.0.0") || !install.Enabled)
                    throw new Exception("Update consent or history UI missing.");
                var selectPage = dialog.GetType().GetMethod("SelectPage");
                selectPage.Invoke(dialog, new object[] { "history" });
                for (int i=0; i<50; i++) versions.Items.Add(release);
                Application.DoEvents();
                var versionFrame = versions.Parent.Parent;
                versionFrame.GetType().GetMethod("Synchronize", fields).Invoke(versionFrame,null);
                var versionThumb = (Control)versionFrame.GetType().GetField("scrollbar", fields).GetValue(versionFrame);
                if (!versionThumb.Visible) throw new Exception("Long version lists need a themed scrollbar.");
                versionThumb.GetType().GetMethod("ChangeValue", fields).Invoke(versionThumb, new object[]{int.MaxValue});
                if (versions.TopIndex == 0) throw new Exception("The custom version scrollbar did not move the native version list.");
                while (versions.Items.Count>2) versions.Items.RemoveAt(versions.Items.Count-1);
                versions.TopIndex=0;
                selectPage.Invoke(dialog, new object[] { "updates" });
                var setNotes = notes.GetType().GetMethod("SetNotes", fields);
                setNotes.Invoke(notes, new object[] { string.Join("\n", Enumerable.Range(0, 160).Select(i => "Scrollable release line " + i)) });
                var scrollFrame = notes.Parent.Parent;
                var syncScroll = scrollFrame.GetType().GetMethod("Synchronize", fields);
                syncScroll.Invoke(scrollFrame, null);
                var scrollThumb = (Control)scrollFrame.GetType().GetField("scrollbar", fields).GetValue(scrollFrame);
                if (!scrollThumb.Visible || notes.Right - SystemInformation.VerticalScrollBarWidth != notes.Parent.ClientSize.Width)
                    throw new Exception("Long release notes must expose the themed thumb and clip only the native scrollbar.");
                int initialTop = notes.GetCharIndexFromPosition(new Point(4, 4));
                scrollThumb.GetType().GetMethod("ChangeValue", fields).Invoke(scrollThumb, new object[] { int.MaxValue });
                Application.DoEvents();
                if (notes.GetCharIndexFromPosition(new Point(4, 4)) <= initialTop)
                    throw new Exception("The custom thumb did not scroll the actual release notes.");
                setNotes.Invoke(notes, new object[] { release.GetType().GetProperty("Notes").GetValue(release) });
                selectPage.Invoke(dialog, new object[] { "general" });
                dialog.Height = 550;
                Application.DoEvents();
                var generalFrame = (Control)dialog.GetType().GetField("generalPage", fields).GetValue(dialog);
                syncScroll.Invoke(generalFrame, null);
                var generalThumb = (Control)generalFrame.GetType().GetField("scrollbar", fields).GetValue(generalFrame);
                if (!generalThumb.Visible) throw new Exception("Compact settings need a visible themed scrollbar.");
                generalThumb.GetType().GetMethod("ChangeValue", fields).Invoke(generalThumb, new object[] { int.MaxValue });
                Application.DoEvents();
                var generalEditor = (ScrollableControl)generalFrame.GetType().GetField("editor", fields).GetValue(generalFrame);
                if (generalEditor.AutoScrollPosition.Y >= 0 || generalEditor.HorizontalScroll.Visible)
                    throw new Exception("Settings scrollbar failed to reach lower controls or introduced horizontal scrolling.");
                dialog.Height = 820;
                selectPage.Invoke(dialog, new object[] { "updates" });
                Application.DoEvents();
                var cancellation = (System.Threading.CancellationTokenSource)panelType.GetField("cancellation", fields).GetValue(panel);
                panelType.GetField("working", fields).SetValue(panel, true);
                foreach (string page in new[] {"general","history","about","updates"})
                {
                    selectPage.Invoke(dialog,new object[]{page});
                    if (Application.OpenForms.Count != 1 || cancellation.IsCancellationRequested
                        || !(bool)panelType.GetField("working",fields).GetValue(panel))
                        throw new Exception("Sidebar navigation opened another window or interrupted an update.");
                }
                panelType.GetField("working", fields).SetValue(panel, false);
                panelType.GetField("installing", fields).SetValue(panel, true);
                dialog.Close();
                if (dialog.IsDisposed || !dialog.Visible) throw new Exception("Closing settings interrupted an active installer.");
                panelType.GetField("installing", fields).SetValue(panel, false);
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
                            selectPage.Invoke(dialog, new object[] { historyPage ? "history" : "updates" });
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
                    selectPage.Invoke(dialog,new object[]{"updates"});
                    dialog.Size = new Size(780, 550);
                    Application.DoEvents();
                    var viewport = dialog.RectangleToScreen(dialog.ClientRectangle);
                    var check = (Button)panelType.GetField("check", fields).GetValue(panel);
                    foreach (Control button in new Control[] { install, check })
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
            var settingsType=app.GetType("RestTimer.SettingsDialog",true);
            var closePrefs=Activator.CreateInstance(app.GetType("RestTimer.Preferences",true),60,900);
            foreach(var choice in new[]{DialogResult.Cancel,DialogResult.No,DialogResult.Yes})
            using(var dialog=(Form)Activator.CreateInstance(settingsType,closePrefs,SystemIcons.Application,(Action)(()=>{}),(Action)(()=>{}),updateService,"general",false))
            {
                dialog.Show();Application.DoEvents();
                var duration=settingsType.GetField("workDuration",flags).GetValue(dialog);
                var seconds=(TextBox)duration.GetType().GetField("seconds",flags).GetValue(duration);
                var daily=(CheckBox)settingsType.GetField("daily",flags).GetValue(dialog);
                bool savedDaily=daily.Checked;
                seconds.Text="05";daily.Checked=!savedDaily;
                var startup=(CheckBox)settingsType.GetField("startupPreference",flags).GetValue(dialog);
                startup.Checked=!startup.Checked;
                settingsType.GetMethod("SelectPage").Invoke(dialog,new object[]{"about"});
                if(CloseChoosing(dialog,choice)!=1) throw new Exception("Closing from another tab did not prompt for pending settings.");
                if((bool)updateState.GetType().GetProperty("DailyEnabled").GetValue(updateState)!=savedDaily)
                    throw new Exception("Discarding or preparing settings changed the live update state before application.");
                if(choice==DialogResult.Cancel)
                {
                    if(!dialog.Visible || !(bool)settingsType.GetProperty("HasPendingChanges").GetValue(dialog)) throw new Exception("Closing the prompt must keep edits open.");
                    seconds.Text="";
                    if(CloseChoosing(dialog,DialogResult.Yes)!=1 || !dialog.Visible || (string)settingsType.GetProperty("CurrentPage").GetValue(dialog)!="general")
                        throw new Exception("Invalid settings must keep the window open on General.");
                }
                else if(choice==DialogResult.Yes)
                {
                    var selected=settingsType.GetProperty("SelectedPreferences").GetValue(dialog);
                    if(selected==null || (int)selected.GetType().GetProperty("WorkSeconds").GetValue(selected)!=65 || dialog.DialogResult!=DialogResult.OK)
                        throw new Exception("Save on close lost pending duration settings.");
                    if((bool)settingsType.GetProperty("SelectedDailyEnabled").GetValue(dialog)==savedDaily)
                        throw new Exception("Save on close lost the pending daily preference.");
                }
                else if(settingsType.GetProperty("SelectedPreferences").GetValue(dialog)!=null || dialog.Visible)
                    throw new Exception("Discard must close without returning settings to apply.");
            }
            using(var dialog=(Form)Activator.CreateInstance(settingsType,closePrefs,SystemIcons.Application,(Action)(()=>{}),(Action)(()=>{}),updateService,"general",false))
            {
                dialog.Show();Application.DoEvents();
                if(CloseChoosing(dialog,DialogResult.No)!=0) throw new Exception("Unchanged settings should close without a prompt.");
            }
            using(var dialog=(Form)Activator.CreateInstance(settingsType,closePrefs,SystemIcons.Application,(Action)(()=>{}),(Action)(()=>{}),updateService,"general",false))
            using(var timer=new System.Windows.Forms.Timer{Interval=30})
            {
                timer.Tick+=delegate {
                    timer.Stop();
                    var duration=settingsType.GetField("workDuration",flags).GetValue(dialog);
                    ((TextBox)duration.GetType().GetField("seconds",flags).GetValue(duration)).Text="07";
                    CloseChoosing(dialog,DialogResult.Yes);
                };
                timer.Start();
                if(dialog.ShowDialog()!=DialogResult.OK || settingsType.GetProperty("SelectedPreferences").GetValue(dialog)==null)
                    throw new Exception("The modal settings window lost the Save result on close.");
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
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /debug:full /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$smokeExe" $smokeSource
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
