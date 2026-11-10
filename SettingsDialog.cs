using System.Drawing;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class SettingsDialog : Form
    {
        private readonly NumericUpDown workMinutes;
        private readonly NumericUpDown breakMinutes;

        public Preferences SelectedPreferences => new Preferences((int)workMinutes.Value, (int)breakMinutes.Value);

        public SettingsDialog(Preferences preferences, Icon icon)
        {
            Text = "تنظیم زمان‌ها — RestTimer";
            Icon = icon;
            Font = SystemFonts.MessageBoxFont;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(350, 166);

            workMinutes = DurationField("مدت کار", preferences.WorkMinutes, Preferences.MaxWorkMinutes, 18);
            breakMinutes = DurationField("مدت استراحت", preferences.BreakMinutes, Preferences.MaxBreakMinutes, 55);
            var note = new Label
            {
                Text = "با تغییر زمان‌ها، شمارش کار از نو شروع می‌شود.",
                AutoSize = true,
                Location = new Point(18, 94)
            };
            var save = new Button
            {
                Text = "ذخیره",
                DialogResult = DialogResult.OK,
                Bounds = new Rectangle(18, 120, 94, 30)
            };
            var cancel = new Button
            {
                Text = "انصراف",
                DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(120, 120, 94, 30)
            };
            Controls.AddRange(new Control[] { note, save, cancel });
            AcceptButton = save;
            CancelButton = cancel;
        }

        private NumericUpDown DurationField(string label, int value, int maximum, int top)
        {
            Controls.Add(new Label
            {
                Text = label + " (دقیقه)",
                AutoSize = true,
                Location = new Point(18, top + 3)
            });
            var input = new NumericUpDown
            {
                AccessibleName = label + " به دقیقه",
                Minimum = 1,
                Maximum = maximum,
                Value = value,
                RightToLeft = RightToLeft.No,
                TextAlign = HorizontalAlignment.Center,
                Bounds = new Rectangle(206, top, 126, 25)
            };
            Controls.Add(input);
            return input;
        }
    }
}
