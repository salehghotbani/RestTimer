using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class SettingsDialog : Form
    {
        private readonly DurationEditor workDuration;
        private readonly DurationEditor breakDuration;
        private readonly Label note;

        public Preferences SelectedPreferences { get; private set; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            const int UseImmersiveDarkMode = 20;
            const int CaptionColor = 35;
            const int TextColor = 36;
            int dark = 1;
            int background = ColorTranslator.ToWin32(Theme.Background);
            int foreground = ColorTranslator.ToWin32(Theme.Text);
            // Older Windows versions ignore unsupported frame attributes.
            DwmSetWindowAttribute(Handle, UseImmersiveDarkMode, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, CaptionColor, ref background, sizeof(int));
            DwmSetWindowAttribute(Handle, TextColor, ref foreground, sizeof(int));
        }

        public SettingsDialog(Preferences preferences, Icon icon)
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
            ClientSize = new Size(536, 490);

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
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(28, 388, 480, 30)
            };
            var save = new RoundedButton(primary: true)
            {
                Text = "ذخیرهٔ زمان‌ها", Bounds = new Rectangle(366, 432, 142, 38), TabIndex = 2
            };
            save.Click += (_, __) => SaveDurations();
            var cancel = new RoundedButton
            {
                Text = "انصراف", DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(28, 432, 100, 38), TabIndex = 3
            };
            Controls.AddRange(new Control[] { title, subtitle, workDuration, breakDuration, note, save, cancel });
            AcceptButton = save;
            CancelButton = cancel;
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
