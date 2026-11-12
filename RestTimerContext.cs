using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class RestTimerContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem status;
        private readonly ToolStripMenuItem startBreak;
        private readonly ToolStripMenuItem settings;
        private readonly ToolStripMenuItem startup;
        private readonly ToolStripMenuItem updates;
        private readonly Icon icon;
        private readonly Timer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private Preferences preferences;
        private BreakOverlay[] overlays = Array.Empty<BreakOverlay>();
        private bool breaking;
        private bool editingSettings;
        private bool breakRequested;

        public RestTimerContext()
        {
            preferences = Preferences.Load();
            status = new ToolStripMenuItem { Enabled = false };
            startBreak = new ToolStripMenuItem("شروع استراحت همین حالا", null, (_, __) => breakRequested = true);
            settings = new ToolStripMenuItem("تنظیم زمان‌ها…", null, (_, __) => EditSettings());
            startup = new ToolStripMenuItem("اجرا با ورود به ویندوز", null, (_, __) => ToggleStartup());
            updates = new ToolStripMenuItem("بررسی به‌روزرسانی…", null, (_, __) => ShowUpdates());
            var exit = new ToolStripMenuItem("خروج", null, (_, __) => ExitThread());
            menu = new ContextMenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                Renderer = new TrayMenuRenderer(),
                Font = Theme.BodyFont,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Padding(6)
            };
            menu.Items.AddRange(new ToolStripItem[]
            {
                status, new ToolStripSeparator(), startBreak, settings, startup, updates, new ToolStripSeparator(), exit
            });
            foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
                item.Padding = new Padding(12, 7, 12, 7);
            menu.Opening += (_, __) => UpdateStartupItem();

            icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            trayIcon = new NotifyIcon { Icon = icon, ContextMenuStrip = menu, Visible = true };
            trayIcon.MouseUp += TrayMouseUp;

            timer = new Timer { Interval = 250 };
            timer.Tick += (_, __) => UpdateTimer();
            UpdateTimer();
            timer.Start();
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        private void TrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            menu.Show(Cursor.Position);
            SetForegroundWindow(menu.Handle);
            menu.Focus();
        }

        private void UpdateTimer()
        {
            if (editingSettings)
                return;

            TimeSpan remaining = TimeSpan.FromSeconds(breaking ? preferences.BreakSeconds : preferences.WorkSeconds)
                - clock.Elapsed;
            if (breaking && remaining <= TimeSpan.Zero)
            {
                BeginWork();
                return;
            }

            if (!breaking && (breakRequested || remaining <= TimeSpan.Zero))
            {
                breakRequested = false;
                menu.Close();
                breaking = true;
                startBreak.Enabled = settings.Enabled = updates.Enabled = false;
                clock.Restart();
                remaining = TimeSpan.FromSeconds(preferences.BreakSeconds);
            }

            var rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
            string countdown = rounded.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            if (rounded.TotalHours >= 1)
                countdown = ((int)rounded.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + countdown;
            status.Text = (breaking ? "پایان استراحت  " : "تا استراحت بعدی  ") + "\u200e" + countdown;
            trayIcon.Text = "RestTimer  " + countdown;

            if (!breaking)
                return;

            Screen[] screens = Screen.AllScreens;
            if (!overlays.Select(overlay => overlay.DisplayBounds).SequenceEqual(screens.Select(screen => screen.Bounds)))
            {
                CloseOverlays();
                overlays = screens.Select(screen => new BreakOverlay(screen)).ToArray();
                foreach (var overlay in overlays)
                {
                    overlay.Skip.Click += (_, __) => BeginWork();
                    overlay.FormClosed += OverlayClosed;
                    overlay.SetCountdown(countdown);
                    overlay.Show();
                }
                overlays.FirstOrDefault(overlay => overlay.IsPrimary)?.Activate();
                return;
            }

            foreach (var overlay in overlays)
                overlay.SetCountdown(countdown);
        }

        private void OverlayClosed(object sender, FormClosedEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
                BeginWork();
        }

        private void BeginWork()
        {
            breaking = false;
            startBreak.Enabled = settings.Enabled = updates.Enabled = true;
            CloseOverlays();
            clock.Restart();
            UpdateTimer();
        }

        private void CloseOverlays()
        {
            foreach (var overlay in overlays)
            {
                overlay.FormClosed -= OverlayClosed;
                if (!overlay.IsDisposed)
                    overlay.Dispose();
            }
            overlays = Array.Empty<BreakOverlay>();
        }

        private void EditSettings()
        {
            if (breaking || editingSettings)
                return;

            editingSettings = true;
            try
            {
                using (var dialog = new SettingsDialog(preferences, icon))
                {
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;

                    var updated = dialog.SelectedPreferences;
                    updated.Save();
                    if (updated.WorkSeconds != preferences.WorkSeconds || updated.BreakSeconds != preferences.BreakSeconds)
                        clock.Restart();
                    preferences = updated;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                ShowSettingsError(ex);
            }
            finally
            {
                editingSettings = false;
            }
        }

        private void ShowUpdates()
        {
            if (breaking || editingSettings)
                return;

            editingSettings = true;
            try
            {
                using (var dialog = new UpdateDialog(icon))
                    dialog.ShowDialog();
            }
            finally
            {
                editingSettings = false;
            }
        }

        private void UpdateStartupItem()
        {
            try
            {
                startup.Checked = Preferences.StartsWithWindows();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                startup.Enabled = false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                Preferences.SetStartup(!Preferences.StartsWithWindows());
                UpdateStartupItem();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                ShowSettingsError(ex);
            }
        }

        private static void ShowSettingsError(Exception ex)
        {
            MessageBox.Show("ذخیرهٔ تنظیمات ممکن نبود.\n" + ex.Message, "RestTimer", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                CloseOverlays();
                trayIcon.Visible = false;
                trayIcon.Dispose();
                menu.Dispose();
                icon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
