using System;
using System.Drawing;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class SettingsDialog : Form
    {
        private readonly DurationEditor workDuration;
        private readonly DurationEditor breakDuration;
        private readonly Label note;
        private readonly Button startTimer;
        private readonly Button stopTimer;
        private readonly Label timerStatus;
        private readonly CheckBox showTaskbarCountdown;
        private readonly ComboBox language;
        private sealed class LanguageItem
        {
            public string Code { get; }
            private readonly string name;
            public LanguageItem(string code, string name) { Code = code; this.name = name; }
            public override string ToString() => name;
        }

        public Preferences SelectedPreferences { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowFrame(Handle);
        }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop)
            : this(preferences, icon, start, stop, null) { }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop, UpdateService updater)
        {
            Text = I18n.T("settings_title") + " — RestTimer";
            Icon = icon;
            Font = Theme.BodyFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            RightToLeft = I18n.Flow;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(536, 660);
            AutoScroll = true;
            AutoScrollMinSize = ClientSize;

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
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 548, 480, 42)
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
                Text = I18n.T("save"), Bounds = new Rectangle(366, 604, 142, 38), TabIndex = 6
            };
            save.Click += (_, __) => SaveDurations();
            var updates = new RoundedButton
            {
                Text = I18n.T("updates_title"),
                Bounds = new Rectangle(144, 604, 206, 38),
                TabIndex = 7
            };
            updates.Click += (_, __) =>
            {
                using (var dialog = new UpdateDialog(icon, updater))
                    dialog.ShowDialog(this);
            };
            var cancel = new RoundedButton
            {
                Text = I18n.T("cancel"), DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(28, 604, 100, 38), TabIndex = 8
            };
            showTaskbarCountdown = new CheckBox { Text = I18n.T("show_countdown"), Checked = preferences.ShowTaskbarCountdown,
                Bounds = new Rectangle(28, 512, 480, 30), TabIndex = 5 };
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
            Controls.AddRange(new Control[] { title, subtitle, workDuration, breakDuration,
                startTimer, stopTimer, timerStatus, languageTitle, language, languageHint, showTaskbarCountdown, note, save, updates, cancel });
            AcceptButton = save;
            CancelButton = cancel;
        }

        public void SetRunning(bool running)
        {
            startTimer.Enabled = !running;
            stopTimer.Enabled = running;
            timerStatus.Text = running ? I18n.T("timer_running") : I18n.T("timer_paused");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Theme.Apply(this);
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height - 24) Height = area.Height - 24;
        }

        private void SaveDurations()
        {
            if (!workDuration.TryGetSeconds(out int work) || work < 1 || work > Preferences.MaxWorkSeconds)
            {
                ShowValidationError(I18n.T("work_validation"));
                return;
            }
            if (!breakDuration.TryGetSeconds(out int rest) || rest < 1 || rest > Preferences.MaxBreakSeconds)
            {
                ShowValidationError(I18n.T("break_validation"));
                return;
            }

            SelectedPreferences = new Preferences(work, rest, showTaskbarCountdown.Checked,
                ((LanguageItem)language.SelectedItem).Code);
            DialogResult = DialogResult.OK;
        }

        private void ShowValidationError(string message)
        {
            note.Text = message;
            note.Tag = "error";
            note.ForeColor = Theme.Error;
        }
    }
}
