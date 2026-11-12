using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class ReleaseNotesBox : RichTextBox
    {
        private string content = "";
        [StructLayout(LayoutKind.Sequential)] private struct ScrollPoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref ScrollPoint point);
        public ReleaseNotesBox()
        {
            Dock = DockStyle.Fill;
            ReadOnly = true;
            BorderStyle = BorderStyle.None;
            DetectUrls = false;
            ScrollBars = RichTextBoxScrollBars.Vertical;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            AccessibleName = "تغییرات نسخه";
        }
        internal void SetNotes(string markdown)
        {
            if (content == (markdown ?? "")) return;
            content = markdown ?? "";
            Rtf = Format(content);
            SelectionStart = 0;
        }
        internal void ApplyPalette()
        {
            var position = new ScrollPoint();
            if (IsHandleCreated) SendMessage(Handle, 0x04DD, IntPtr.Zero, ref position); // EM_GETSCROLLPOS
            int start = SelectionStart, length = SelectionLength;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Rtf = Format(content);
            Select(Math.Min(start, TextLength), Math.Min(length, Math.Max(0, TextLength - start)));
            if (IsHandleCreated) SendMessage(Handle, 0x04DE, IntPtr.Zero, ref position); // EM_SETSCROLLPOS
        }
        internal static string Format(string markdown)
        {
            var color = Theme.Text;
            var rtf = new StringBuilder(@"{\rtf1\ansi\deff0\uc1{\fonttbl{\f0 Segoe UI;}}{\colortbl ;\red")
                .Append(color.R).Append(@"\green").Append(color.G).Append(@"\blue").Append(color.B).Append(@";}\viewkind4\f0\cf1 ");
            bool beginning = true;
            foreach (string raw in (markdown ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                string plain = line.TrimStart('#', ' ', '*');
                if (line.Length == 0) continue;
                if (beginning && (plain == "تغییرات RestTimer" || Regex.IsMatch(plain, @"^(?:RestTimer\s+[vV]?\d+(?:\.\d+){1,3}|نسخه[ٔ\s]*[\d٫.]+(?:\s+RestTimer)?)$"))) continue;
                beginning = false;
                bool heading = line.StartsWith("#") || Regex.IsMatch(line, @"^[A-Za-z ]+:$");
                line = Regex.Replace(plain, @"\[([^\]]+)\]\([^)]*\)", "$1").Replace("**", "").Replace("`", "");
                if (line.StartsWith("- ")) line = "•  " + line.Substring(2);
                bool rtl = Regex.IsMatch(line, @"[\u0600-\u06ff]");
                rtf.Append(rtl ? @"\pard\rtlpar\qr " : @"\pard\ltrpar\ql ");
                rtf.Append(heading ? @"\b\fs24\sb100\sa100 " : @"\b0\fs20\sb0\sa90 ").Append(@"\sl270\slmult1 ");
                foreach (char character in line)
                {
                    if (character == '\\' || character == '{' || character == '}') rtf.Append('\\').Append(character);
                    else if (character <= 127) rtf.Append(character);
                    else rtf.Append(@"\u").Append(((short)character).ToString(CultureInfo.InvariantCulture)).Append('?');
                }
                rtf.Append(@"\par ");
            }
            return rtf.Append('}').ToString();
        }
    }
    internal sealed class UpdateProgress : Control
    {
        private int value;
        public int Value { get => value; set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public UpdateProgress() { DoubleBuffered = true; Height = 6; AccessibleName = "پیشرفت دانلود"; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Input);
            using (var brush = new SolidBrush(Theme.Mint)) e.Graphics.FillRectangle(brush, Width - Width * value / 100, 0, Width * value / 100, Height);
        }
    }
    internal sealed class ThemedCheckBox : CheckBox
    {
        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            float scale = DeviceDpi / 96f;
            int side = (int)(16 * scale), gap = (int)(10 * scale);
            bool rtl = RightToLeft == RightToLeft.Yes;
            var box = new Rectangle(rtl ? Width - side - 1 : 1, (Height - side) / 2, side, side);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRectangle(box, 4 * scale))
            using (var brush = new SolidBrush(Checked ? Theme.Mint : Theme.Input))
            using (var border = new Pen(Checked ? Theme.Mint : Theme.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
            if (Checked)
                using (var pen = new Pen(Theme.OnAccent, 2 * scale))
                    e.Graphics.DrawLines(pen, new[] { new PointF(box.Left + 3 * scale, box.Top + 8 * scale),
                        new PointF(box.Left + 7 * scale, box.Top + 12 * scale), new PointF(box.Left + 13 * scale, box.Top + 4 * scale) });
            var text = rtl ? new Rectangle(0, 0, Width - side - gap, Height) : new Rectangle(side + gap, 0, Width - side - gap, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left));
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, text, Theme.Text, Theme.Background);
        }
    }
}
