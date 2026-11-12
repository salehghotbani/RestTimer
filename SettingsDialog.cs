using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class SettingsDialog : Form
    {
        private DurationEditor workDuration, breakDuration;
        private Label note, timerStatus;
        private Button startTimer, stopTimer, savePreferences;
        private CheckBox showTaskbarCountdown, startupPreference, daily;
        private ComboBox language;
        private readonly UpdateService service;
        private readonly UpdatePanel updatePanel;
        private readonly Panel content;
        private readonly Control generalPage, aboutPage;
        private readonly Dictionary<string, RoundedButton> navigation = new Dictionary<string, RoundedButton>();
        private readonly Bitmap logo;
        private readonly Timer themeTimer;
        private string paletteKey;
        private readonly Preferences originalPreferences;
        private readonly bool originalDaily;
        private bool originalStartup, saving;
        public Preferences SelectedPreferences { get; private set; }
        public string CurrentPage { get; private set; }
        public bool SelectedDailyEnabled => daily.Checked;
        public bool? SelectedStartupEnabled => startupPreference.Enabled ? (bool?)startupPreference.Checked : null;
        public bool StartupChanged => startupPreference.Enabled && startupPreference.Checked != originalStartup;
        public bool HasPendingChanges => !workDuration.TryGetSeconds(out int work) || !breakDuration.TryGetSeconds(out int rest)
            || work != originalPreferences.WorkSeconds || rest != originalPreferences.BreakSeconds
            || showTaskbarCountdown.Checked != originalPreferences.ShowTaskbarCountdown
            || ((LanguageItem)language.SelectedItem).Code != originalPreferences.Language
            || daily.Checked != originalDaily || StartupChanged;

        private sealed class LanguageItem
        {
            public string Code { get; }
            private readonly string name;
            public LanguageItem(string code, string name) { Code = code; this.name = name; }
            public override string ToString() => name;
        }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop)
            : this(preferences, icon, start, stop, null) { }
        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop, UpdateService updater)
            : this(preferences, icon, start, stop, updater, "general", true) { }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop, UpdateService updater,
            string initialPage, bool checkOnShow)
        {
            service = updater ?? new UpdateService();
            originalPreferences = preferences;
            originalDaily = service.State.DailyEnabled;
            Theme.Refresh();
            Text = "RestTimer — " + I18n.T("preferences_title");
            Icon = icon;
            logo = icon.ToBitmap();
            Font = Theme.BodyFont;
            RightToLeft = I18n.Flow;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(960, 780);
            MinimumSize = new Size(780, 500);
            MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var sidebar = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10),
                Margin = I18n.IsRtl ? new Padding(16, 0, 0, 0) : new Padding(0, 0, 16, 0) };
            var tabs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, AccessibleName = I18n.T("settings_navigation") };
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            for (int i = 0; i < 4; i++) tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tabs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var brand = new Panel { Dock = DockStyle.Fill };
            brand.Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(56, 0, 36, 36) });
            brand.Controls.Add(new Label { Text = "RestTimer", Font = Theme.TitleFont, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 40, 148, 30) });
            tabs.Controls.Add(brand, 0, 0);
            string[] pages = { "general", "updates", "history", "about" };
            string[] keys = { "general", "updates_tab", "version_history", "about" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                var button = new RoundedButton { Text = I18n.T(keys[i]), Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), TabIndex = i };
                button.Click += (_, __) => SelectPage(page);
                navigation.Add(page, button);
                tabs.Controls.Add(button, 0, i + 1);
            }
            tabs.Controls.Add(new Label { Text = "v" + UpdateClient.CurrentVersion.ToString(3), Font = Theme.SmallFont,
                Tag = "muted", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No }, 0, 6);
            sidebar.Controls.Add(tabs);
            content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            generalPage = CreateGeneralPage(preferences, start, stop);
            aboutPage = CreateAboutPage();
            updatePanel = new UpdatePanel(service, checkOnShow) { Dock = DockStyle.Fill };
            updatePanel.DailyPreferenceChanged += () => daily.Checked = updatePanel.DailyEnabled;
            content.Controls.AddRange(new Control[] { generalPage, updatePanel, aboutPage });
            layout.Controls.Add(sidebar, 0, 0);
            layout.Controls.Add(content, 1, 0);
            Controls.Add(layout);
            SelectPage(initialPage);
            ApplyTheme();
            themeTimer = new Timer { Interval = 750 };
            themeTimer.Tick += (_, __) => { Theme.Refresh(); if (paletteKey != Theme.PaletteKey) ApplyTheme(); };
            themeTimer.Start();
        }

        private Control CreateGeneralPage(Preferences preferences, Action start, Action stop)
        {
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var body = new Panel { Size = new Size(536, 730), RightToLeft = I18n.Flow };
            viewport.Controls.Add(body);
            viewport.AutoScrollMinSize = body.Size;
            viewport.Resize += (_, __) =>
            {
                body.Width = Math.Max(508, Math.Min(536, viewport.ClientSize.Width));
                viewport.AutoScrollMinSize = body.Size;
                body.Left = Math.Max(0, (viewport.ClientSize.Width - body.Width) / 2);
            };
            var title = new Label
            {
                Text = I18n.T("settings_title"), Font = Theme.HeadingFont,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 24, 480, 36)
            };
            var subtitle = new Label
            {
                Text = I18n.T("settings_subtitle"), Tag = "muted",
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(28, 65, 480, 26)
            };
            workDuration = new DurationEditor(I18n.T("work_duration"), I18n.T("work_hint"), preferences.WorkSeconds)
            {
                Location = new Point(28, 106), TabIndex = 0
            };
            breakDuration = new DurationEditor(I18n.T("break_duration"), I18n.T("break_hint"), preferences.BreakSeconds)
            {
                Location = new Point(28, 248), TabIndex = 1
            };
            note = new Label
            {
                Text = I18n.T("duration_note"), Tag = "muted",
                Font = Theme.SmallFont, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 620, 480, 42)
            };
            startTimer = new RoundedButton(primary: true) { Text = I18n.T("start"), Bounds = new Rectangle(408, 388, 100, 34), TabIndex = 2 };
            stopTimer = new RoundedButton { Text = I18n.T("stop"), Bounds = new Rectangle(296, 388, 100, 34), TabIndex = 3 };
            startTimer.Click += (_, __) => start();
            stopTimer.Click += (_, __) => stop();
            timerStatus = new Label
            {
                Font = Theme.SmallFont, ForeColor = Theme.Muted, Tag = "muted",
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 388, 244, 34)
            };
            var save = new RoundedButton(primary: true)
            {
                Text = I18n.T("save"), Bounds = new Rectangle(366, 678, 142, 38), TabIndex = 6
            };
            save.Click += (_, __) => SaveDurations();
            showTaskbarCountdown = new CheckBox { Text = I18n.T("show_countdown"), Checked = preferences.ShowTaskbarCountdown,
                Bounds = new Rectangle(28, 584, 480, 30), TabIndex = 5 };
            var languageTitle = new Label { Text = I18n.T("language"), TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(28, 434, 130, 32) };
            language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(170, 434, 338, 32),
                TabIndex = 4, AccessibleName = I18n.T("language"), RightToLeft = RightToLeft.No, DropDownHeight = 320,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Input, ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Font.Height + 6 };
            language.Items.Add(new LanguageItem("auto", I18n.T("system_language")));
            foreach (string code in I18n.SupportedCodes) language.Items.Add(new LanguageItem(code, I18n.NativeName(code)));
            language.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var brush = new SolidBrush(selected ? Theme.Selection : Theme.Input)) e.Graphics.FillRectangle(brush, e.Bounds);
                var item = (LanguageItem)language.Items[e.Index];
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
                if (item.Code == "fa" || item.Code == "ar" || item.Code == "auto" && I18n.IsRtl) flags |= TextFormatFlags.RightToLeft;
                TextRenderer.DrawText(e.Graphics, item.ToString(), Font, Rectangle.Inflate(e.Bounds, -5, 0), selected ? Theme.OnSelection : Theme.Text, flags);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            };
            for (int i = 0; i < language.Items.Count; i++)
                if (((LanguageItem)language.Items[i]).Code == preferences.Language) language.SelectedIndex = i;
            var languageHint = new Label { Text = I18n.T("language_hint"), Font = Theme.SmallFont, Tag = "muted",
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 472, 480, 34) };
            daily = new ThemedCheckBox { Text = I18n.T("daily_check"), Checked = service.State.DailyEnabled,
                Bounds = new Rectangle(28, 510, 480, 34) };
            daily.CheckedChanged += (_, __) =>
            {
                updatePanel?.SyncDailyPreference(daily.Checked);
            };
            startupPreference = new ThemedCheckBox { Text = I18n.T("startup"), Bounds = new Rectangle(28, 548, 480, 30) };
            try { startupPreference.Checked = Preferences.StartsWithWindows(); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { startupPreference.Enabled = false; }
            originalStartup = startupPreference.Checked;
            body.Controls.AddRange(new Control[] { title, subtitle, workDuration, breakDuration,
                startTimer, stopTimer, timerStatus, languageTitle, language, languageHint, daily, startupPreference, showTaskbarCountdown, note, save });
            savePreferences = save;
            return new ScrollFrame(viewport);
        }

        public void SetRunning(bool running)
        {
            startTimer.Enabled = !running;
            stopTimer.Enabled = running;
            timerStatus.Text = running ? I18n.T("timer_running") : I18n.T("timer_paused");
        }

        private Control CreateAboutPage()
        {
            var page = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(28) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 84, 48, 36, 96, 38, 38, 46 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            layout.Controls.Add(new Label { Text = "RestTimer", Font = Theme.HeadingFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
            layout.Controls.Add(new Label { Text = I18n.T("installed", UpdateClient.CurrentVersion.ToString(3)), Tag = "accent",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 2);
            layout.Controls.Add(new Label { Text = I18n.T("about_description"), Tag = "muted", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 3);
            layout.Controls.Add(new Label { Text = I18n.T("author") + ": \u200eSaleh Ghotbani", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 4);
            layout.Controls.Add(new Label { Text = I18n.T("license") + ": MIT", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 5);
            var project = new RoundedButton { Text = I18n.T("project_site"), Dock = DockStyle.Fill, Margin = new Padding(20, 4, 20, 4) };
            project.Click += (_, __) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/salehghotbani/RestTimer") { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                { MessageBox.Show(this, I18n.T("open_link_error"), "RestTimer", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            layout.Controls.Add(project, 0, 6);
            page.Controls.Add(layout);
            return page;
        }

        public void SelectPage(string page)
        {
            if (!navigation.ContainsKey(page)) page = "general";
            CurrentPage = page;
            generalPage.Visible = page == "general";
            aboutPage.Visible = page == "about";
            updatePanel.Visible = page == "updates" || page == "history";
            updatePanel.SelectPage(page == "history");
            foreach (var pair in navigation) { pair.Value.Selected = pair.Key == page; pair.Value.Invalidate(); }
            AcceptButton = page == "general" ? (IButtonControl)savePreferences : null;
            if (Visible && updatePanel.Visible) updatePanel.EnsureLoaded();
        }

        private void ApplyTheme() { Theme.Apply(this); paletteKey = Theme.PaletteKey; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindowFrame(Handle); }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyTheme();
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height - 24) Height = area.Height - 24;
            if (Width > area.Width - 24) Width = area.Width - 24;
            SelectPage(CurrentPage);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (updatePanel.Installing) e.Cancel = true;
                else if (!saving && HasPendingChanges)
                {
                    using (var prompt = new UnsavedChangesDialog(Icon))
                    {
                        var choice = prompt.ShowDialog(this);
                        if (choice == DialogResult.Yes)
                        {
                            if (SelectPreferences()) { saving = true; DialogResult = DialogResult.OK; }
                            else { SelectPage("general"); e.Cancel = true; }
                        }
                        else if (choice != DialogResult.No) e.Cancel = true;
                    }
                }
            }
            base.OnFormClosing(e);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) themeTimer?.Dispose();
            base.Dispose(disposing);
            if (disposing) logo?.Dispose();
        }

        private void SaveDurations()
        {
            if (updatePanel.Installing) { SelectPage("updates"); return; }
            if (!SelectPreferences()) return;
            saving = true;
            DialogResult = DialogResult.OK;
        }
        private bool SelectPreferences()
        {
            if (!workDuration.TryGetSeconds(out int work) || work < 1 || work > Preferences.MaxWorkSeconds)
            {
                ShowValidationError(I18n.T("work_validation"));
                return false;
            }
            if (!breakDuration.TryGetSeconds(out int rest) || rest < 1 || rest > Preferences.MaxBreakSeconds)
            {
                ShowValidationError(I18n.T("break_validation"));
                return false;
            }

            SelectedPreferences = new Preferences(work, rest, showTaskbarCountdown.Checked,
                ((LanguageItem)language.SelectedItem).Code);
            return true;
        }

        private void ShowValidationError(string message)
        {
            note.Text = message;
            note.Tag = "error";
            note.ForeColor = Theme.Error;
        }
    }
}
