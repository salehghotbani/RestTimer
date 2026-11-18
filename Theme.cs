using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RestTimer
{
    internal static class Theme
    {
        internal static bool? LightOverride { get; set; }
        public static void Use(string mode) { LightOverride = mode == "light" ? (bool?)true : mode == "dark" ? (bool?)false : null; Refresh(); }
        public static bool IsLight { get; private set; } = ReadLightMode();
        private static bool highContrast = SystemInformation.HighContrast;
        public static string PaletteKey => IsLight + ":" + highContrast + ":" + SystemColors.WindowText.ToArgb();
        public static Color Background => highContrast ? SystemColors.Window : IsLight ? Color.FromArgb(244, 247, 250) : Color.FromArgb(13, 21, 33);
        public static Color Surface => highContrast ? SystemColors.Window : IsLight ? Color.White : Color.FromArgb(20, 31, 46);
        public static Color Input => highContrast ? SystemColors.Control : IsLight ? Color.FromArgb(234, 240, 245) : Color.FromArgb(12, 22, 35);
        public static Color Border => highContrast ? SystemColors.WindowText : IsLight ? Color.FromArgb(205, 216, 226) : Color.FromArgb(44, 62, 82);
        public static Color Text => highContrast ? SystemColors.WindowText : IsLight ? Color.FromArgb(26, 40, 56) : Color.FromArgb(237, 244, 250);
        public static Color Muted => highContrast ? SystemColors.WindowText : IsLight ? Color.FromArgb(83, 103, 120) : Color.FromArgb(155, 176, 195);
        public static Color Mint => highContrast ? SystemColors.Highlight : IsLight ? Color.FromArgb(17, 112, 91) : Color.FromArgb(131, 230, 196);
        public static Color OnAccent => highContrast ? SystemColors.HighlightText : IsLight ? Color.White : Color.FromArgb(13, 21, 33);
        public static Color OnSelection => highContrast ? SystemColors.HighlightText : Mint;
        public static Color AccentText => highContrast ? SystemColors.WindowText : Mint;
        public static Color Error => highContrast ? SystemColors.WindowText : IsLight ? Color.FromArgb(174, 42, 42) : Color.FromArgb(255, 164, 155);
        public static Color Selection => highContrast ? SystemColors.Highlight : IsLight ? Color.FromArgb(219, 238, 232) : Color.FromArgb(34, 58, 72);
        public static Color ButtonFill => highContrast ? SystemColors.Control : IsLight ? Color.FromArgb(231, 237, 243) : Color.FromArgb(30, 46, 64);
        public static readonly Font HeadingFont = new Font("Segoe UI", 17, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI", 12, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 10);
        public static readonly Font SmallFont = new Font("Segoe UI", 9);

        private static bool ReadLightMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return !(key?.GetValue("AppsUseLightTheme") is int light) || light != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException) { return true; }
        }
        public static bool Refresh()
        {
            string before = PaletteKey;
            IsLight = LightOverride ?? ReadLightMode();
            highContrast = SystemInformation.HighContrast;
            return before != PaletteKey;
        }
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string app, string id);
        public static void Apply(Control control)
        {
            if (control.IsDisposed) return;
            if (control is Form) control.BackColor = Background;
            else if (control is UpdateProgress) control.BackColor = Input;
            else if (control is ToolStrip || control is ListBox || control is RichTextBox) control.BackColor = Surface;
            else if (control is TextBoxBase || control is ComboBox || control is NumericUpDown) control.BackColor = Input;
            else if (control is DashboardChart || control is DateTimePicker || control is ListView) control.BackColor = Surface;
            else if (control.Tag as string == "border") control.BackColor = Border;
            else control.BackColor = Color.Transparent;
            control.ForeColor = control.Tag as string == "muted" ? Muted : control.Tag as string == "accent" ? AccentText : control.Tag as string == "error" ? Error : Text;
            if (control is RoundedPanel panel) { panel.FillColor = panel.Tag as string == "input" ? Input : Surface; panel.BorderColor = Border; }
            if (control is ReleaseNotesBox notes) notes.ApplyPalette();
            if (control.IsHandleCreated)
            {
                if (control is Form) ApplyWindowFrame(control.Handle);
                else if (control is RichTextBox || control is ListBox || control is ComboBox || control is ListView || control is DateTimePicker) SetWindowTheme(control.Handle, IsLight || highContrast ? null : "DarkMode_Explorer", null);
            }
            // The native up/down buttons do not support transparent backgrounds;
            // NumericUpDown propagates its own palette to its editor.
            if (!(control is NumericUpDown)) foreach (Control child in control.Controls) Apply(child);
            control.Invalidate();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public static void ApplyWindowFrame(IntPtr window)
        {
            const int UseImmersiveDarkMode = 20;
            const int CaptionColor = 35;
            const int TextColor = 36;
            int dark = IsLight || highContrast ? 0 : 1;
            int background = ColorTranslator.ToWin32(Background);
            int foreground = ColorTranslator.ToWin32(Text);
            // Older Windows versions ignore unsupported frame attributes.
            DwmSetWindowAttribute(window, UseImmersiveDarkMode, ref dark, sizeof(int));
            DwmSetWindowAttribute(window, CaptionColor, ref background, sizeof(int));
            DwmSetWindowAttribute(window, TextColor, ref foreground, sizeof(int));
        }

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
        public bool Selected { get; set; }
        public string Glyph { get; set; }
        public bool IconOnly { get; set; }
        public bool Quiet { get; set; }
        public bool StartAligned { get; set; }

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

            Color fill = primary ? Theme.Mint : Selected ? Theme.Selection : Quiet ? Theme.Surface : Theme.ButtonFill;
            Color text = primary ? Theme.OnAccent : Selected ? Theme.OnSelection : Theme.Text;
            Color borderColor = Quiet ? fill : primary || Selected ? Theme.Mint : Theme.Border;
            if (!Enabled)
            {
                fill = Quiet ? Theme.Surface : Theme.Input;
                text = Theme.Muted;
                borderColor = Quiet ? Theme.Surface : Theme.Border;
            }
            else if (hovered || pressed)
                fill = ControlPaint.Light(fill, pressed ? 0.08f : 0.16f);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9 * DeviceDpi / 96f))
            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(borderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
            var flags = (StartAligned && !IconOnly ? (RightToLeft == RightToLeft.Yes ? TextFormatFlags.Right : TextFormatFlags.Left) : TextFormatFlags.HorizontalCenter)
                | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            if (RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft;
            var glyph = Glyph ?? UiIcons.ForText(Name, Text);
            int imageSize = Math.Min(Height - 10, (int)(18 * DeviceDpi / 96f));
            int imageGap = (int)(6 * DeviceDpi / 96f);
            if (Image != null || glyph != null)
            {
                int textWidth = IconOnly ? 0 : TextRenderer.MeasureText(Text, Font, Size.Empty, flags).Width;
                int groupWidth = Math.Min(Width - 12, textWidth + imageSize + (IconOnly ? 0 : imageGap));
                bool rtl = RightToLeft == RightToLeft.Yes;
                int left = StartAligned && !IconOnly ? (rtl ? Width - 12 - groupWidth : 12) : (Width - groupWidth) / 2;
                int imageX = rtl ? left + groupWidth - imageSize : left;
                var imageBounds = new Rectangle(imageX, (Height-imageSize)/2, imageSize, imageSize);
                if(Image != null) e.Graphics.DrawImage(Image, imageBounds);
                else UiIcons.Draw(e.Graphics, imageBounds, glyph, text);
                var labelBounds = new Rectangle(rtl ? left : left + imageSize + imageGap, 0, Math.Max(1,groupWidth-imageSize-imageGap), Height);
                if (!IconOnly) TextRenderer.DrawText(e.Graphics, Text, Font, labelBounds, text, flags | TextFormatFlags.EndEllipsis);
            }
            else TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ForeColor, fill);
        }
    }
}
