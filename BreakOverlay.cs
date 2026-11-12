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
        private readonly Bitmap background;
        private readonly RoundedPanel card;
        private readonly Panel separator;
        private readonly Label heading;
        private readonly Label countdown;
        private readonly Label countdownCaption;
        private readonly Label caption;
        private readonly Font headingFont = new Font("Segoe UI", 14, FontStyle.Bold);
        private readonly Font countdownFont = new Font("Segoe UI", 30, FontStyle.Bold);

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
            BackColor = Theme.Background;
            Bounds = DisplayBounds;

            card = new RoundedPanel { CornerRadius = 18 };
            separator = new Panel { BackColor = Theme.Border };
            heading = CenteredLabel("وقت استراحت است", headingFont, Theme.Text, RightToLeft.Yes);
            heading.TextAlign = ContentAlignment.MiddleLeft;
            countdown = CenteredLabel("", countdownFont, Theme.Mint, RightToLeft.No);
            countdown.AccessibleName = "زمان باقی‌ماندهٔ استراحت";
            countdownCaption = CenteredLabel("زمان باقی‌مانده", Theme.SmallFont, Theme.Muted, RightToLeft.Yes);
            caption = CenteredLabel("چند دقیقه از صفحه فاصله بگیر.", Theme.BodyFont, Theme.Muted, RightToLeft.Yes);
            caption.TextAlign = ContentAlignment.MiddleLeft;
            Skip = new RoundedButton
            {
                Text = "رد کردن استراحت",
                Font = Theme.SmallFont,
                RightToLeft = RightToLeft.Yes
            };
            card.Controls.AddRange(new Control[] { separator, heading, countdown, countdownCaption, caption, Skip });
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
            using (var shade = new SolidBrush(Color.FromArgb(110, BackColor)))
                e.Graphics.FillRectangle(shade, ClientRectangle);
        }

        private void LayoutCard()
        {
            int Scale(int units) => (units * DeviceDpi + 48) / 96;

            int width = Math.Max(1, Math.Min(Scale(720), ClientSize.Width - Scale(32)));
            int top = Math.Max(0, Math.Min(Scale(24), ClientSize.Height - Scale(108)));
            int padding = Scale(20);
            int buttonWidth = Math.Min(Scale(112), width / 4);
            int timerWidth = Math.Min(Scale(168), width / 3);
            int timerLeft = padding + buttonWidth + Scale(32);
            int textLeft = timerLeft + timerWidth + Scale(24);
            int textWidth = Math.Max(1, width - textLeft - padding);
            bool compact = width < Scale(600);

            card.SetBounds((ClientSize.Width - width) / 2, top, width, Scale(108));
            Skip.SetBounds(padding, Scale(36), buttonWidth, Scale(36));
            separator.SetBounds(padding + buttonWidth + Scale(16), Scale(32), Scale(1), Scale(44));
            countdown.SetBounds(timerLeft, Scale(compact ? 24 : 15), timerWidth, Scale(54));
            countdownCaption.SetBounds(timerLeft, Scale(69), timerWidth, Scale(22));
            countdownCaption.Visible = !compact;
            heading.SetBounds(textLeft, Scale(compact ? 21 : 22), textWidth, Scale(compact ? 66 : 32));
            caption.SetBounds(textLeft, Scale(57), textWidth, Scale(28));
            caption.Visible = !compact;
        }

        private static Label CenteredLabel(string text, Font font, Color color, RightToLeft direction) =>
            new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter, RightToLeft = direction };

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

                    double scale = Math.Min(1, 240.0 / Math.Max(bounds.Width, bounds.Height));
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
            }
        }
    }
}
