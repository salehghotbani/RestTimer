using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
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
        private readonly ToolTip hints = new ToolTip();
        private readonly Font headingFont = new Font("Segoe UI", 14, FontStyle.Bold);
        private readonly Font countdownFont = new Font("Segoe UI", 30, FontStyle.Bold);

        public Rectangle DisplayBounds { get; }
        public bool IsPrimary { get; }
        public Button Skip { get; }
        public Button Start { get; }
        public Button Stop { get; }
        public Button Reset { get; }

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
            heading = CenteredLabel(I18n.T("break_heading"), headingFont, Theme.Text, I18n.Flow);
            heading.TextAlign = ContentAlignment.MiddleLeft;
            countdown = CenteredLabel("", countdownFont, Theme.Mint, RightToLeft.No);
            countdown.AccessibleName = I18n.T("remaining_break");
            countdownCaption = CenteredLabel(I18n.T("remaining"), Theme.SmallFont, Theme.Muted, I18n.Flow);
            caption = CenteredLabel(I18n.T("break_caption"), Theme.BodyFont, Theme.Muted, I18n.Flow);
            caption.TextAlign = ContentAlignment.MiddleLeft;
            Skip = new RoundedButton
            {
                Text = I18n.T("skip"),
                Font = Theme.SmallFont,
                RightToLeft = I18n.Flow
            };
            Start = new RoundedButton(primary: true) { Text = I18n.T("start"), Font = Theme.SmallFont, RightToLeft = I18n.Flow };
            Stop = new RoundedButton { Text = I18n.T("stop"), Font = Theme.SmallFont, RightToLeft = I18n.Flow };
            Reset = new RoundedButton { Text = I18n.T("reset_timer"), Font = Theme.SmallFont, RightToLeft = I18n.Flow };
            foreach (var button in new[] {Start,Stop,Reset,Skip}) hints.SetToolTip(button,button.Text);
            card.Controls.AddRange(new Control[] { separator, heading, countdown, countdownCaption, caption, Skip, Start, Stop, Reset });
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

        public void SetRunning(bool running)
        {
            Start.Enabled = !running;
            Stop.Enabled = running;
            heading.Text = running ? I18n.T("break_heading") : I18n.T("break_paused");
            countdownCaption.Text = running ? I18n.T("remaining") : I18n.T("timer_paused");
            LayoutCard();
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

            int width = Math.Max(1, Math.Min(Scale(920), ClientSize.Width - Scale(24)));
            int top = Math.Max(0, Math.Min(Scale(16), ClientSize.Height - Scale(96)));
            int padding = Scale(12), gap = Scale(6);
            bool compact = width < Scale(680);
            var buttons = new[] { Start, Stop, Reset };
            var sizes = buttons.Select(button => compact ? Scale(32) : Math.Max(Scale(76), ((RoundedButton)button).LabeledWidth)).ToArray();
            int buttonWidth = sizes.Sum() + 2 * gap;
            int timerWidth = Math.Min(Math.Max(Scale(148),TextRenderer.MeasureText("00:00",countdown.Font).Width+Scale(8)), width / 3);
            int timerLeft = padding + buttonWidth + Scale(24);
            int textLeft = timerLeft + timerWidth + Scale(16);
            int textWidth = Math.Max(1, width - textLeft - padding);
            int countHeight=Math.Max(Scale(54),TextRenderer.MeasureText("00:00",countdown.Font).Height+Scale(2));
            int timerCaptionHeight=Math.Max(Scale(22),TextRenderer.MeasureText(countdownCaption.Text,countdownCaption.Font,new Size(timerWidth,32767),TextFormatFlags.WordBreak).Height+Scale(2));
            int headingHeight=Math.Max(Scale(32),TextRenderer.MeasureText(heading.Text,heading.Font,new Size(textWidth,32767),TextFormatFlags.WordBreak).Height+Scale(2));
            int captionHeight=Math.Max(Scale(28),TextRenderer.MeasureText(caption.Text,caption.Font,new Size(textWidth,32767),TextFormatFlags.WordBreak).Height+Scale(2));
            int height=Math.Max(Scale(96),Math.Max(Scale(8)+countHeight+(compact?0:timerCaptionHeight)+padding,Scale(14)+headingHeight+(compact?0:Scale(4)+captionHeight)+padding));
            card.SetBounds((ClientSize.Width-width)/2, top, width, height);
            int x=padding;
            for(int i=0;i<buttons.Length;i++)
            {
                ((RoundedButton)buttons[i]).IconOnly=compact;
                buttons[i].SetBounds(x,Scale(12),sizes[i],Scale(30));x+=sizes[i]+gap;
            }
            ((RoundedButton)Skip).IconOnly=((RoundedButton)Skip).LabeledWidth>buttonWidth;
            Skip.SetBounds(padding,Scale(50),buttonWidth,Scale(32));
            separator.SetBounds(padding+buttonWidth+Scale(12),Scale(22),Scale(1),Scale(50));
            countdown.SetBounds(timerLeft,Scale(8),timerWidth,countHeight);
            countdownCaption.SetBounds(timerLeft,Scale(8)+countHeight,timerWidth,timerCaptionHeight);
            countdownCaption.Visible=!compact;
            heading.SetBounds(textLeft,Scale(14),textWidth,headingHeight);
            heading.Visible=width>=Scale(420);
            caption.SetBounds(textLeft,Scale(14)+headingHeight+Scale(4),textWidth,captionHeight);caption.Visible=!compact;

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
                hints.Dispose();
            }
        }
    }
}
