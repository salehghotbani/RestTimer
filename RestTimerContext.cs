using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private readonly ToolStripMenuItem resetTimer;
        private readonly ToolStripMenuItem settings;
        private readonly ToolStripMenuItem startup;
        private readonly ToolStripMenuItem updates;
        private readonly Icon icon;
        private readonly Timer timer;
        private readonly TaskbarCountdown taskbarCountdown;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly UpdateService updater = new UpdateService();
        private readonly bool enableAutomaticUpdates;
        private readonly System.Threading.CancellationTokenSource updateCancellation = new System.Threading.CancellationTokenSource();
        private readonly System.Threading.EventWaitHandle shutdownRequest = new System.Threading.EventWaitHandle(
            false, System.Threading.EventResetMode.AutoReset, Program.ShutdownEventName);
        private Preferences preferences;
        private BreakOverlay[] overlays = Array.Empty<BreakOverlay>();
        private bool breaking;
        private string taskName, segmentId;
        private int? sessionSeconds;
        private DateTimeOffset segmentStart;
        private readonly Stopwatch workClock = new Stopwatch();
        private readonly WorkJournal journal = new WorkJournal();
        private bool editingSettings;
        private bool breakRequested;
        private bool disposed;
        private DateTime nextThemeCheck;
        private string paletteKey;
        private SettingsDialog settingsDialog;
        private bool sidebarCollapsed;

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
            resetTimer = new ToolStripMenuItem(I18n.T("reset_timer"), null, (_, __) => ResetTimer());
            settings = new ToolStripMenuItem(I18n.T("dashboard"), null, (_, __) => ShowDashboard());
            startup = new ToolStripMenuItem(I18n.T("startup"), null, (_, __) => ToggleStartup());
            updates = new ToolStripMenuItem(I18n.T("updates_menu"), null, (_, __) => ShowUpdates());
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
                settings, new ToolStripSeparator(), startTimer, stopTimer, startBreak, resetTimer, new ToolStripSeparator(), status,
                new ToolStripSeparator(), startup, updates, new ToolStripSeparator(), exit
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
                {
                    clock.Stop();
                    if (FinishSegment(false)) ExitThread();
                }
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

        private void TrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            menu.Close();
            ShowDashboard();
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

            TimeSpan remaining = TimeSpan.FromSeconds(breaking ? preferences.BreakSeconds : (sessionSeconds ?? preferences.WorkSeconds))
                - clock.Elapsed;
            if (!editingSettings && running && breaking && remaining <= TimeSpan.Zero)
            {
                BeginWork();
                return;
            }

            if (!editingSettings && !breaking && (breakRequested || (running && remaining <= TimeSpan.Zero)))
            {
                if (!FinishSegment(false)) { clock.Stop(); return; }
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
            if (!breaking && !string.IsNullOrEmpty(taskName)) status.Text = taskName + " · " + status.Text;
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
                    overlay.Reset.Click += (_, __) => ResetTimer();
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
            if (segmentId != null && !workClock.IsRunning && !FinishSegment(true)) return;
            clock.Start();
            StartSegment();
            UpdateTimer();
        }

        private void StopTimer()
        {
            breakRequested = false;
            clock.Stop();
            FinishSegment(true);
            UpdateTimer();
        }

        private void ResetClock()
        {
            bool running = clock.IsRunning;
            clock.Reset();
            if (running)
                clock.Start();
        }

        private void StartSegment()
        {
            if (breaking || string.IsNullOrEmpty(taskName) || workClock.IsRunning) return;
            if (segmentId == null) { segmentId = Guid.NewGuid().ToString("N"); segmentStart = DateTimeOffset.UtcNow; }
            workClock.Start();
        }
        private bool FinishSegment(bool ask)
        {
            if (segmentId == null) return true;
            workClock.Stop();
            bool save = true;
            if (ask)
                using (var prompt = new UnsavedChangesDialog(icon, "save_work", "save_work_message"))
                {
                    prompt.Describe(taskName + "  ·  " + WorkJournal.Format(workClock.Elapsed.TotalSeconds));
                    var choice = prompt.ShowDialog(settingsDialog);
                    if (choice != DialogResult.Yes && choice != DialogResult.No) return false;
                    save = choice == DialogResult.Yes;
                }
            try { if (save) journal.Save(segmentId, taskName, segmentStart, workClock.Elapsed.TotalSeconds); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { MessageBox.Show(I18n.T("log_error"), "RestTimer"); return false; }
            segmentId = null; workClock.Reset(); return true;
        }
        private void ResetTimer()
        {
            if (breaking) { breakRequested = false; ResetClock(); UpdateTimer(); return; }
            bool previousEditing = editingSettings;
            editingSettings = true;
            try {
                using (var dialog = new WorkStartDialog(sessionSeconds ?? preferences.WorkSeconds, taskName))
                {
                    if (dialog.ShowDialog(settingsDialog) != DialogResult.OK)
                    { return; }
                    clock.Stop();
                    if (!FinishSegment(true)) return;
                    taskName = dialog.TaskName; sessionSeconds = dialog.Seconds;
                    breakRequested = false; clock.Restart(); StartSegment();
                }
            } finally { editingSettings = previousEditing; UpdateTimer(); }
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
            if (clock.IsRunning) StartSegment();
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

        private void ShowDashboard()
        {
            OpenPreferences("report");
        }

        private void OpenPreferences(string page)
        {
            if (settingsDialog != null && !settingsDialog.IsDisposed)
            {
                settingsDialog.SelectPage(page);
                if (settingsDialog.WindowState == FormWindowState.Minimized) settingsDialog.WindowState = FormWindowState.Normal;
                if (!settingsDialog.Visible) settingsDialog.Show();
                settingsDialog.Activate();
                return;
            }
            if (breaking || editingSettings)
                return;

            editingSettings = true;
            try
            {
                using (var dialog = new SettingsDialog(preferences, icon, StartTimer, StopTimer, updater, page, true))
                {
                    settingsDialog = dialog;
                    dialog.SetSidebarCollapsed(sidebarCollapsed);
                    dialog.LanguagePreviewChanged += RefreshLanguage;
                    dialog.RestartRequested += ResetTimer;
                    dialog.SetRunning(clock.IsRunning);
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;

                    var updated = dialog.SelectedPreferences;
                    updated.Save();
                    if (dialog.StartupChanged) Preferences.SetStartup(dialog.SelectedStartupEnabled.Value);
                    updater.State.DailyEnabled = dialog.SelectedDailyEnabled;
                    updater.Save();
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
                if (!disposed) ApplyLanguage();
                if (settingsDialog != null) sidebarCollapsed = settingsDialog.SidebarCollapsed;
                settingsDialog = null;
                editingSettings = false;
            }
        }

        private void ApplyLanguage()
        {
            I18n.Use(preferences.Language);
            RefreshLanguage();
        }
        private void RefreshLanguage()
        {
            menu.RightToLeft = I18n.Flow;
            string[] keys = { "dashboard", "start", "stop", "start_break", "reset_timer", "startup", "updates_menu", "quit" };
            var items = menu.Items.OfType<ToolStripMenuItem>().Where(item => item != status).ToArray();
            for (int i = 0; i < keys.Length; i++) items[i].Text = I18n.T(keys[i]);
            if (UpdateClient.IsStable(updater.State.Latest) && updater.State.Latest.Version > UpdateClient.CurrentVersion)
                updates.Text = I18n.T("update_available", updater.State.Latest.Version.ToString(3));
        }

        private async System.Threading.Tasks.Task CheckDailyUpdatesAsync()
        {
            if (!enableAutomaticUpdates || updater.Checking || !updater.State.IsDue(DateTime.UtcNow)) return;
            try { await updater.CheckAsync(updateCancellation.Token).ConfigureAwait(false); }
            catch (Exception ex) when (UpdatePanel.ExpectedFailure(ex)) { }
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
            OpenPreferences(history ? "history" : "updates");
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

        protected override void ExitThreadCore()
        {
            clock.Stop();
            if (!FinishSegment(true)) { UpdateTimer(); return; }
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                FinishSegment(false);
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
