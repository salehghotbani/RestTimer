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
        private readonly ToolStripMenuItem startTimer;
        private readonly ToolStripMenuItem stopTimer;
        private readonly ToolStripMenuItem startBreak;
        private readonly ToolStripMenuItem settings;
        private readonly ToolStripMenuItem startup;
        private readonly ToolStripMenuItem updates;
        private readonly Icon icon;
        private readonly Timer timer;
        private readonly TaskbarCountdown taskbarCountdown;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly UpdateService updater = new UpdateService();
        private readonly bool enableAutomaticUpdates;
        private readonly System.Threading.CancellationTokenSource updateCancellation = new System.Threading.CancellationTokenSource();
        private readonly System.Threading.EventWaitHandle shutdownRequest = new System.Threading.EventWaitHandle(
            false, System.Threading.EventResetMode.AutoReset, Program.ShutdownEventName);
        private Preferences preferences;
        private BreakOverlay[] overlays = Array.Empty<BreakOverlay>();
        private bool breaking;
        private bool editingSettings;
        private bool breakRequested;
        private bool disposed;
        private DateTime nextThemeCheck;
        private string paletteKey;
        private SettingsDialog settingsDialog;

        public RestTimerContext() : this(true) { }

        public RestTimerContext(bool enableAutomaticUpdates) : this(enableAutomaticUpdates, true) { }

        public RestTimerContext(bool enableAutomaticUpdates, bool enableTaskbarCountdown)
        {
            this.enableAutomaticUpdates = enableAutomaticUpdates;
            preferences = Preferences.Load();
            I18n.Use(preferences.Language);
            if (enableTaskbarCountdown) taskbarCountdown = new TaskbarCountdown();
            status = new ToolStripMenuItem { Enabled = false };
            startTimer = new ToolStripMenuItem(I18n.T("start"), null, (_, __) => StartTimer());
            stopTimer = new ToolStripMenuItem(I18n.T("stop"), null, (_, __) => StopTimer());
            startBreak = new ToolStripMenuItem(I18n.T("start_break"), null, (_, __) => breakRequested = true);
            settings = new ToolStripMenuItem(I18n.T("settings_menu"), null, (_, __) => EditSettings());
            startup = new ToolStripMenuItem(I18n.T("startup"), null, (_, __) => ToggleStartup());
            updates = new ToolStripMenuItem(I18n.T("updates_menu"), null, (_, __) => ShowUpdates());
            var history = new ToolStripMenuItem(I18n.T("history"), null, (_, __) => ShowUpdates(true));
            var exit = new ToolStripMenuItem(I18n.T("quit"), null, (_, __) => ExitThread());
            menu = new ContextMenuStrip
            {
                RightToLeft = I18n.Flow,
                Renderer = new TrayMenuRenderer(),
                Font = Theme.SmallFont,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Padding(2)
            };
            menu.Items.AddRange(new ToolStripItem[]
            {
                startTimer, stopTimer, startBreak, new ToolStripSeparator(), status,
                new ToolStripSeparator(), settings, startup, updates, history, new ToolStripSeparator(), exit
            });
            foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
                item.Padding = new Padding(6, 2, 6, 2);
            menu.Opening += (_, __) => { Theme.Refresh(); Theme.Apply(menu); UpdateStartupItem(); };

            icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            trayIcon = new NotifyIcon { Icon = icon, ContextMenuStrip = menu, Visible = true };
            trayIcon.MouseUp += TrayMouseUp;
            trayIcon.BalloonTipClicked += (_, __) => ShowUpdates();

            timer = new Timer { Interval = 250 };
            timer.Tick += (_, __) =>
            {
                if (shutdownRequest.WaitOne(0))
                    ExitThread();
                else
                {
                    UpdateTimer();
                    _ = CheckDailyUpdatesAsync();
                    NotifyUpdate();
                }
            };
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
            if (DateTime.UtcNow >= nextThemeCheck)
            {
                nextThemeCheck = DateTime.UtcNow.AddSeconds(1);
                Theme.Refresh();
                if (paletteKey != Theme.PaletteKey)
                {
                    paletteKey = Theme.PaletteKey;
                    Theme.Apply(menu);
                    foreach (Form form in Application.OpenForms)
                        if (form is SettingsDialog) Theme.Apply(form);
                }
            }
            bool running = clock.IsRunning;
            startTimer.Enabled = !running;
            stopTimer.Enabled = running;
            settingsDialog?.SetRunning(running);

            TimeSpan remaining = TimeSpan.FromSeconds(breaking ? preferences.BreakSeconds : preferences.WorkSeconds)
                - clock.Elapsed;
            if (!editingSettings && running && breaking && remaining <= TimeSpan.Zero)
            {
                BeginWork();
                return;
            }

            if (!editingSettings && !breaking && (breakRequested || (running && remaining <= TimeSpan.Zero)))
            {
                breakRequested = false;
                menu.Close();
                breaking = true;
                startBreak.Enabled = settings.Enabled = updates.Enabled = false;
                clock.Restart();
                running = true;
                startTimer.Enabled = false;
                stopTimer.Enabled = true;
                remaining = TimeSpan.FromSeconds(preferences.BreakSeconds);
            }

            var rounded = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(remaining.TotalSeconds)));
            string countdown = rounded.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            if (rounded.TotalHours >= 1)
                countdown = ((int)rounded.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + countdown;
            status.Text = I18n.T(breaking ? "end_break" : "next_break") + "  \u200e" + countdown;
            if (!running)
                status.Text = I18n.T("paused") + " · " + status.Text;
            trayIcon.Text = (running ? "RestTimer  " : "RestTimer · " + I18n.T("paused") + "  ") + countdown;
            taskbarCountdown?.UpdateDisplay(countdown, breaking, running, preferences.ShowTaskbarCountdown);

            if (editingSettings) return;

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
                    overlay.Start.Click += (_, __) => StartTimer();
                    overlay.Stop.Click += (_, __) => StopTimer();
                    overlay.FormClosed += OverlayClosed;
                    overlay.SetCountdown(countdown);
                    overlay.SetRunning(running);
                    overlay.Show();
                }
                overlays.FirstOrDefault(overlay => overlay.IsPrimary)?.Activate();
                return;
            }

            foreach (var overlay in overlays)
            {
                overlay.SetCountdown(countdown);
                overlay.SetRunning(running);
            }
        }

        private void StartTimer()
        {
            clock.Start();
            UpdateTimer();
        }

        private void StopTimer()
        {
            breakRequested = false;
            clock.Stop();
            UpdateTimer();
        }

        private void ResetClock()
        {
            bool running = clock.IsRunning;
            clock.Reset();
            if (running)
                clock.Start();
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
            ResetClock();
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
                using (var dialog = new SettingsDialog(preferences, icon, StartTimer, StopTimer, updater))
                {
                    settingsDialog = dialog;
                    dialog.SetRunning(clock.IsRunning);
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;

                    var updated = dialog.SelectedPreferences;
                    updated.Save();
                    if (updated.WorkSeconds != preferences.WorkSeconds || updated.BreakSeconds != preferences.BreakSeconds)
                        ResetClock();
                    preferences = updated;
                    ApplyLanguage();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                ShowSettingsError(ex);
            }
            finally
            {
                settingsDialog = null;
                editingSettings = false;
            }
        }

        private void ApplyLanguage()
        {
            I18n.Use(preferences.Language);
            menu.RightToLeft = I18n.Flow;
            string[] keys = { "start", "stop", "start_break", "settings_menu", "startup", "updates_menu", "history", "quit" };
            var items = menu.Items.OfType<ToolStripMenuItem>().Where(item => item != status).ToArray();
            for (int i = 0; i < keys.Length; i++) items[i].Text = I18n.T(keys[i]);
        }

        private async System.Threading.Tasks.Task CheckDailyUpdatesAsync()
        {
            if (!enableAutomaticUpdates || updater.Checking || !updater.State.IsDue(DateTime.UtcNow)) return;
            try { await updater.CheckAsync(updateCancellation.Token).ConfigureAwait(false); }
            catch (Exception ex) when (UpdateDialog.ExpectedFailure(ex)) { }
        }

        private void NotifyUpdate()
        {
            var release = updater.State.Latest;
            if (breaking || editingSettings || !UpdateClient.IsStable(release) || release.Version <= UpdateClient.CurrentVersion) return;
            updates.Text = I18n.T("update_available", release.Version.ToString(3));
            if (!updater.State.DailyEnabled) return;
            if (updater.State.NotifiedTag == release.Tag) return;
            trayIcon.ShowBalloonTip(10000, I18n.T("update_title"), I18n.T("update_notification", release.Version.ToString(3)), ToolTipIcon.Info);
            updater.State.NotifiedTag = release.Tag;
            updater.Save();
        }

        private void ShowUpdates(bool history = false)
        {
            if (breaking || editingSettings)
                return;

            editingSettings = true;
            try
            {
                using (var dialog = new UpdateDialog(icon, updater, history))
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
            MessageBox.Show(I18n.T("save_error") + "\n" + ex.Message, "RestTimer", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, I18n.MessageOptions);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                timer.Dispose();
                updateCancellation.Cancel();
                updateCancellation.Dispose();
                shutdownRequest.Dispose();
                CloseOverlays();
                trayIcon.Visible = false;
                trayIcon.Dispose();
                menu.Dispose();
                icon?.Dispose();
                taskbarCountdown?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
