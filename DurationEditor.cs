using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class DurationEditor : RoundedPanel
    {
        private readonly TextBox hours;
        private readonly TextBox minutes;
        private readonly TextBox seconds;
        private readonly Font digitsFont = new Font("Segoe UI", 20);
        public void Relocalize(string title, string hint)
        {
            var titleLabel = (Label)Controls[0];
            var hintLabel = (Label)Controls[1];
            float scale = DeviceDpi / 96f;
            titleLabel.Text = title; hintLabel.Text = hint;
            titleLabel.RightToLeft = hintLabel.RightToLeft = I18n.Flow;
            var titleBounds = I18n.IsRtl ? new Rectangle(300, 10, 160, 27) : new Rectangle(20, 10, 230, 27);
            var hintBounds = I18n.IsRtl ? new Rectangle(20, 10, 265, 27) : new Rectangle(260, 10, 200, 27);
            titleLabel.Bounds = new Rectangle((int)(titleBounds.X * scale), (int)(titleBounds.Y * scale), (int)(titleBounds.Width * scale), (int)(titleBounds.Height * scale));
            hintLabel.Bounds = new Rectangle((int)(hintBounds.X * scale), (int)(hintBounds.Y * scale), (int)(hintBounds.Width * scale), (int)(hintBounds.Height * scale));
            var inputs = new[] { hours, minutes, seconds };
            string[] keys = { "hours", "minutes", "seconds" };
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].AccessibleName = title + " – " + I18n.T(keys[i]);
                foreach (Control control in Controls)
                    if (control is Label caption && caption.Top > titleLabel.Bottom && caption.Left == inputs[i].Parent.Left)
                    { caption.Text = I18n.T(keys[i]); caption.RightToLeft = I18n.Flow; }
            }
        }

        public DurationEditor(string title, string hint, int duration)
        {
            Size = new Size(480, 128);
            RightToLeft = RightToLeft.No;
            Controls.Add(new Label
            {
                Text = title, Font = Theme.TitleFont, ForeColor = Theme.Text,
                BackColor = Color.Transparent, RightToLeft = I18n.Flow,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = I18n.IsRtl ? new Rectangle(300, 10, 160, 27) : new Rectangle(20, 10, 230, 27)
            });
            Controls.Add(new Label
            {
                Text = hint, Tag = "muted", Font = Theme.SmallFont, ForeColor = Theme.Muted,
                BackColor = Color.Transparent, RightToLeft = I18n.Flow,
                TextAlign = ContentAlignment.MiddleRight, Bounds = I18n.IsRtl ? new Rectangle(20, 10, 265, 27) : new Rectangle(260, 10, 200, 27)
            });

            hours = CreatePart(I18n.T("hours"), title + " – " + I18n.T("hours"), duration / 3600, 74, 99);
            minutes = CreatePart(I18n.T("minutes"), title + " – " + I18n.T("minutes"), duration / 60 % 60, 194, 59);
            seconds = CreatePart(I18n.T("seconds"), title + " – " + I18n.T("seconds"), duration % 60, 314, 59);
            foreach (int left in new[] { 166, 286 })
                Controls.Add(new Label
                {
                    Text = ":", Tag = "muted", Font = digitsFont, ForeColor = Theme.Muted, BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(left, 42, 28, 48)
                });
        }

        private TextBox CreatePart(string label, string accessibleName, int value, int left, int maximum)
        {
            var field = new RoundedPanel
            {
                FillColor = Theme.Input, Tag = "input", CornerRadius = 10, Bounds = new Rectangle(left, 44, 92, 48)
            };
            var input = new TextBox
            {
                Text = value.ToString("00", CultureInfo.InvariantCulture),
                AccessibleName = accessibleName, Font = digitsFont, ForeColor = Theme.Text,
                BackColor = Theme.Input, BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center, RightToLeft = RightToLeft.No,
                MaxLength = 2, Bounds = new Rectangle(10, 6, 72, 36)
            };
            string previous = input.Text;
            bool restoring = false;
            input.TextChanged += (_, __) =>
            {
                if (restoring) return;
                if (input.Text.Length == 0 || TryReadPart(input, out int number) && number <= maximum)
                    previous = input.Text;
                else
                {
                    int caret = input.SelectionStart;
                    restoring = true;
                    input.Text = previous;
                    input.SelectionStart = System.Math.Min(caret, input.TextLength);
                    restoring = false;
                }
            };
            input.Enter += (_, __) =>
            {
                field.BorderColor = Theme.Mint;
                field.Invalidate();
            };
            input.Leave += (_, __) =>
            {
                field.BorderColor = Theme.Border;
                field.Invalidate();
                if (TryReadPart(input, out int number))
                    input.Text = number.ToString("00", CultureInfo.InvariantCulture);
            };
            field.Controls.Add(input);
            Controls.Add(field);
            Controls.Add(new Label
            {
                Text = label, Tag = "muted", Font = Theme.SmallFont, ForeColor = Theme.Muted, BackColor = Color.Transparent,
                RightToLeft = I18n.Flow, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(left, 96, 92, 22)
            });
            return input;
        }

        public bool TryGetSeconds(out int duration)
        {
            duration = 0;
            if (!TryReadPart(hours, out int h) || !TryReadPart(minutes, out int m)
                || !TryReadPart(seconds, out int s) || m > 59 || s > 59)
                return false;

            duration = h * 3600 + m * 60 + s;
            return true;
        }

        private static bool TryReadPart(TextBox input, out int value)
        {
            char[] digits = input.Text.Trim().ToCharArray();
            // Accept both Persian and Arabic numerals, including pasted values.
            for (int i = 0; i < digits.Length; i++)
            {
                if (digits[i] >= '۰' && digits[i] <= '۹')
                    digits[i] = (char)('0' + digits[i] - '۰');
                else if (digits[i] >= '٠' && digits[i] <= '٩')
                    digits[i] = (char)('0' + digits[i] - '٠');
            }
            return int.TryParse(new string(digits), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                digitsFont.Dispose();
        }
    }
}
