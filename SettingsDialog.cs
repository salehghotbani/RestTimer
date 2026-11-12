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

        public Preferences SelectedPreferences { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowFrame(Handle);
        }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop)
        {
            Text = "زمان‌بندی استراحت — RestTimer";
            Icon = icon;
            Font = Theme.BodyFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            RightToLeft = RightToLeft.Yes;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(536, 536);

            var title = new Label
            {
                Text = "زمان‌بندی استراحت", Font = Theme.HeadingFont,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 24, 480, 36)
            };
            var subtitle = new Label
            {
                Text = "زمان هر دوره را با ساعت، دقیقه و ثانیه تنظیم کن.",
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(28, 65, 480, 26)
            };
            workDuration = new DurationEditor("زمان کار", "تا استراحت بعدی", preferences.WorkSeconds)
            {
                Location = new Point(28, 106), TabIndex = 0
            };
            breakDuration = new DurationEditor("زمان استراحت", "فرصتی برای تجدید انرژی", preferences.BreakSeconds)
            {
                Location = new Point(28, 248), TabIndex = 1
            };
            note = new Label
            {
                Text = "با تغییر زمان‌ها، شمارش کار از نو شروع می‌شود.",
                Font = Theme.SmallFont, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 434, 480, 30)
            };
            startTimer = new RoundedButton(primary: true) { Text = "شروع", Bounds = new Rectangle(408, 388, 100, 34), TabIndex = 2 };
            stopTimer = new RoundedButton { Text = "توقف", Bounds = new Rectangle(296, 388, 100, 34), TabIndex = 3 };
            startTimer.Click += (_, __) => start();
            stopTimer.Click += (_, __) => stop();
            timerStatus = new Label
            {
                Font = Theme.SmallFont, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 388, 244, 34)
            };
            var save = new RoundedButton(primary: true)
            {
                Text = "ذخیرهٔ زمان‌ها", Bounds = new Rectangle(366, 478, 142, 38), TabIndex = 4
            };
            save.Click += (_, __) => SaveDurations();
            var updates = new RoundedButton
            {
                Text = "بررسی به‌روزرسانی",
                Bounds = new Rectangle(144, 478, 206, 38),
                TabIndex = 5
            };
            updates.Click += (_, __) =>
            {
                using (var dialog = new UpdateDialog(icon))
                    dialog.ShowDialog(this);
            };
            var cancel = new RoundedButton
            {
                Text = "انصراف", DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(28, 478, 100, 38), TabIndex = 6
            };
            Controls.AddRange(new Control[] { title, subtitle, workDuration, breakDuration,
                startTimer, stopTimer, timerStatus, note, save, updates, cancel });
            AcceptButton = save;
            CancelButton = cancel;
        }

        public void SetRunning(bool running)
        {
            startTimer.Enabled = !running;
            stopTimer.Enabled = running;
            timerStatus.Text = running ? "تایمر در حال اجراست" : "تایمر متوقف است";
        }

        private void SaveDurations()
        {
            if (!workDuration.TryGetSeconds(out int work) || work < 1 || work > Preferences.MaxWorkSeconds)
            {
                ShowValidationError("زمان کار باید از ۱ ثانیه تا ۲۴ ساعت باشد. دقیقه و ثانیه حداکثر ۵۹ هستند.");
                return;
            }
            if (!breakDuration.TryGetSeconds(out int rest) || rest < 1 || rest > Preferences.MaxBreakSeconds)
            {
                ShowValidationError("زمان استراحت باید از ۱ ثانیه تا ۲ ساعت باشد. دقیقه و ثانیه حداکثر ۵۹ هستند.");
                return;
            }

            SelectedPreferences = new Preferences(work, rest);
            DialogResult = DialogResult.OK;
        }

        private void ShowValidationError(string message)
        {
            note.Text = message;
            note.ForeColor = Color.FromArgb(255, 164, 155);
        }
    }
}
