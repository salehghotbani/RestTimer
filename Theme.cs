using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RestTimer
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(13, 21, 33);
        public static readonly Color Surface = Color.FromArgb(20, 31, 46);
        public static readonly Color Input = Color.FromArgb(12, 22, 35);
        public static readonly Color Border = Color.FromArgb(44, 62, 82);
        public static readonly Color Text = Color.FromArgb(237, 244, 250);
        public static readonly Color Muted = Color.FromArgb(155, 176, 195);
        public static readonly Color Mint = Color.FromArgb(131, 230, 196);
        public static readonly Font HeadingFont = new Font("Segoe UI", 17, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI", 12, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 10);
        public static readonly Font SmallFont = new Font("Segoe UI", 9);

        public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = System.Math.Min(radius * 2, System.Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class RoundedPanel : Panel
    {
        public Color FillColor { get; set; } = Theme.Surface;
        public Color BorderColor { get; set; } = Theme.Border;
        public int CornerRadius { get; set; } = 16;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 2 || Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1),
                CornerRadius * DeviceDpi / 96f))
            using (var fill = new SolidBrush(FillColor))
            using (var border = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private readonly bool primary;
        private bool hovered;
        private bool pressed;

        public RoundedButton(bool primary = false)
        {
            this.primary = primary;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            ForeColor = primary ? Theme.Background : Theme.Text;
            Font = Theme.BodyFont;
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            hovered = pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = e.Button == MouseButtons.Left;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(System.EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(System.EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 2 || Height < 2)
                return;

            Color fill = primary ? Theme.Mint : Color.FromArgb(30, 46, 64);
            if (hovered || pressed)
                fill = ControlPaint.Light(fill, pressed ? 0.08f : 0.16f);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9 * DeviceDpi / 96f))
            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(primary ? Theme.Mint : Theme.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            if (RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ForeColor, fill);
        }
    }
}
