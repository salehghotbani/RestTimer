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

        public DurationEditor(string title, string hint, int duration)
        {
            Size = new Size(480, 128);
            RightToLeft = RightToLeft.No;
            Controls.Add(new Label
            {
                Text = title, Font = Theme.TitleFont, ForeColor = Theme.Text,
                BackColor = Color.Transparent, RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(300, 10, 160, 27)
            });
            Controls.Add(new Label
            {
                Text = hint, Font = Theme.SmallFont, ForeColor = Theme.Muted,
                BackColor = Color.Transparent, RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight, Bounds = new Rectangle(20, 10, 265, 27)
            });

            hours = CreatePart("ساعت", title + "، ساعت", duration / 3600, 74);
            minutes = CreatePart("دقیقه", title + "، دقیقه", duration / 60 % 60, 194);
            seconds = CreatePart("ثانیه", title + "، ثانیه", duration % 60, 314);
            foreach (int left in new[] { 166, 286 })
                Controls.Add(new Label
                {
                    Text = ":", Font = digitsFont, ForeColor = Theme.Muted, BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(left, 42, 28, 48)
                });
        }

        private TextBox CreatePart(string label, string accessibleName, int value, int left)
        {
            var field = new RoundedPanel
            {
                FillColor = Theme.Input, CornerRadius = 10, Bounds = new Rectangle(left, 44, 92, 48)
            };
            var input = new TextBox
            {
                Text = value.ToString("00", CultureInfo.InvariantCulture),
                AccessibleName = accessibleName, Font = digitsFont, ForeColor = Theme.Text,
                BackColor = Theme.Input, BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center, RightToLeft = RightToLeft.No,
                MaxLength = 2, Bounds = new Rectangle(10, 6, 72, 36)
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
                Text = label, Font = Theme.SmallFont, ForeColor = Theme.Muted, BackColor = Color.Transparent,
                RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.MiddleCenter,
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
