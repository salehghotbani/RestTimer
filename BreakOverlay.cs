using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class BreakOverlay : Form
    {
        private static readonly Color Ink = Color.FromArgb(239, 244, 250);
        private static readonly Color Mint = Color.FromArgb(131, 230, 196);
        private readonly Bitmap background;
        private readonly Panel card;
        private readonly Panel accent;
        private readonly Label heading;
        private readonly Label countdown;
        private readonly Label caption;
        private readonly Font headingFont = new Font("Segoe UI", 19, FontStyle.Bold);
        private readonly Font countdownFont = new Font("Segoe UI", 44, FontStyle.Bold);
        private readonly Font bodyFont = new Font("Segoe UI", 11, FontStyle.Regular);

        public Rectangle DisplayBounds { get; }
        public bool IsPrimary { get; }
        public Button Skip { get; }

        public BreakOverlay(Screen screen)
        {
            DisplayBounds = screen.Bounds;
            IsPrimary = screen.Primary;
            background = CaptureBlurredDesktop(DisplayBounds);

            Text = "RestTimer";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(14, 20, 29);
            Bounds = DisplayBounds;

            card = new Panel { BackColor = Color.FromArgb(19, 29, 44) };
            accent = new Panel { BackColor = Mint };
            heading = CenteredLabel("وقت استراحت است", headingFont, Ink, RightToLeft.Yes);
            countdown = CenteredLabel("", countdownFont, Mint, RightToLeft.No);
            countdown.AccessibleName = "زمان باقی‌ماندهٔ استراحت";
            caption = CenteredLabel("چند دقیقه از صفحه فاصله بگیر.", bodyFont, Color.FromArgb(174, 189, 207), RightToLeft.Yes);
            Skip = new Button
            {
                Text = "رد کردن استراحت",
                Font = bodyFont,
                RightToLeft = RightToLeft.Yes,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 49, 69),
                ForeColor = Ink
            };
            Skip.FlatAppearance.BorderColor = Color.FromArgb(78, 102, 127);
            Skip.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 72, 97);
            card.Controls.AddRange(new Control[] { accent, heading, countdown, caption, Skip });
            Controls.Add(card);
            SizeChanged += (_, __) => LayoutCard();
            DpiChanged += (_, __) => LayoutCard();
        }

        protected override bool ShowWithoutActivation => !IsPrimary;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = DisplayBounds;
            LayoutCard();
        }

        public void SetCountdown(string value)
        {
            if (countdown.Text != value)
                countdown.Text = value;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData != Keys.Escape)
                return base.ProcessCmdKey(ref msg, keyData);

            Skip.PerformClick();
            return true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (background == null)
            {
                base.OnPaintBackground(e);
                return;
            }

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(background, ClientRectangle);
            using (var shade = new SolidBrush(Color.FromArgb(175, BackColor)))
                e.Graphics.FillRectangle(shade, ClientRectangle);
        }

        private void LayoutCard()
        {
            int Scale(int units) => (units * DeviceDpi + 48) / 96;

            int width = Math.Max(1, Math.Min(Scale(480), ClientSize.Width - Scale(24)));
            int top = Math.Max(0, Math.Min(Scale(36), ClientSize.Height - Scale(256)));
            card.SetBounds((ClientSize.Width - width) / 2, top, width, Scale(252));
            accent.SetBounds(0, 0, width, Scale(4));
            heading.SetBounds(0, Scale(22), width, Scale(40));
            countdown.SetBounds(0, Scale(62), width, Scale(92));
            caption.SetBounds(0, Scale(152), width, Scale(30));
            int buttonWidth = Math.Min(width, Scale(176));
            Skip.SetBounds((width - buttonWidth) / 2, Scale(195), buttonWidth, Scale(38));
        }

        private static Label CenteredLabel(string text, Font font, Color color, RightToLeft direction) =>
            new Label { Text = text, Font = font, ForeColor = color, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = direction };

        private static Bitmap CaptureBlurredDesktop(Rectangle bounds)
        {
            // Keep only a low-resolution copy in memory. Interpolating it back to
            // screen size blurs the desktop without keeping a full-size capture.
            try
            {
                using (var desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb))
                {
                    using (var capture = Graphics.FromImage(desktop))
                        capture.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    double scale = 64.0 / Math.Max(bounds.Width, bounds.Height);
                    var blurred = new Bitmap(Math.Max(1, (int)(bounds.Width * scale)),
                        Math.Max(1, (int)(bounds.Height * scale)), PixelFormat.Format32bppRgb);
                    using (var graphics = Graphics.FromImage(blurred))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(desktop, new Rectangle(Point.Empty, blurred.Size));
                    }
                    return blurred;
                }
            }
            catch (Win32Exception)
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                background?.Dispose();
                headingFont.Dispose();
                countdownFont.Dispose();
                bodyFont.Dispose();
            }
        }
    }
}
