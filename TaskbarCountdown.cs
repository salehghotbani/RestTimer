using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RestTimer
{
    // A separate, non-activating window: Explorer's taskbar is never modified.
    internal sealed class TaskbarCountdown : NativeWindow, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int Width, Height; public NativeSize(int width, int height) { Width = width; Height = height; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, Alpha, Format; }
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
            ref NativePoint destination, ref NativeSize size, IntPtr sourceDC, ref NativePoint source, uint colorKey, ref Blend blend, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int maximum);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect bounds, int size);
        private string countdown = "1:00:00";
        private bool resting, running = true;
        private DateTime nextPosition;
        private bool lightMode, highContrast;
        private Rectangle taskbarBounds, trayBounds, screenBounds;
        private float displayScale = 1;
        private bool dirty = true;
        private string languageCode;
        internal bool FrameUploaded { get; private set; }
        public Rectangle Bounds { get; private set; }
        public Size Size => Bounds.Size;
        public bool Visible => Handle != IntPtr.Zero && IsWindowVisible(Handle);
        public string AccessibleName { get; private set; }
        public string AccessibleDescription { get; private set; }

        public TaskbarCountdown()
        {
            AccessibleName = I18n.T("countdown_accessible");
            // A display-only native popup avoids Form.Show() selecting the form
            // when it returns after fullscreen. It is revealed only by a native
            // SetWindowPos call with SWP_NOACTIVATE.
            CreateHandle(new CreateParams { Caption = "RestTimer countdown", Width = 1, Height = 1,
                Style = unchecked((int)0x88000000), // WS_POPUP | WS_DISABLED
                ExStyle = 0x08000000 | 0x00000080 | 0x00000020 | 0x00080000 | 0x00000008 });
        }
        public void Hide() { if (Handle != IntPtr.Zero) ShowWindow(Handle, 0); }
        public void Dispose() { if (Handle != IntPtr.Zero) DestroyHandle(); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; } // Click-through, including dragging.
            if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (message.Msg == 0x001A || message.Msg == 0x031A || message.Msg == 0x007E || message.Msg == 0x02E0)
            { dirty = true; nextPosition = DateTime.MinValue; }
            base.WndProc(ref message);
        }

        internal static Rectangle Place(Rectangle taskbar, Rectangle tray, Rectangle screen, Size size, float scale)
        {
            int gap = Math.Max(4, (int)Math.Round(6 * scale));
            if (taskbar.Width < 1 || taskbar.Height < 1 || size.Width < 1 || size.Height < 1
                || screen.Width < size.Width + 2 * gap || screen.Height < size.Height + 2 * gap) return Rectangle.Empty;
            int x, y;
            if (taskbar.Width > taskbar.Height)
            {
                x = tray.Width > 0 ? tray.Left - gap - size.Width : screen.Right - gap - size.Width;
                y = taskbar.Top + taskbar.Height / 2 < screen.Top + screen.Height / 2
                    ? taskbar.Bottom + gap : taskbar.Top - gap - size.Height;
            }
            else
            {
                x = taskbar.Left + taskbar.Width / 2 < screen.Left + screen.Width / 2
                    ? taskbar.Right + gap : taskbar.Left - gap - size.Width;
                y = screen.Bottom - gap - size.Height;
            }
            x = Math.Max(screen.Left + gap, Math.Min(x, screen.Right - gap - size.Width));
            y = Math.Max(screen.Top + gap, Math.Min(y, screen.Bottom - gap - size.Height));
            var result = new Rectangle(x, y, size.Width, size.Height);
            return screen.Contains(result) && !taskbar.IntersectsWith(result) ? result : Rectangle.Empty;
        }

        public void UpdateDisplay(string text, bool isResting, bool isRunning, bool enabled)
        {
            if (countdown != text || resting != isResting || running != isRunning || languageCode != I18n.Code)
            {
                languageCode = I18n.Code;
                AccessibleName = I18n.T("countdown_accessible");
                countdown = text; resting = isResting; running = isRunning;
                AccessibleDescription = I18n.T(resting ? "break_short" : "work_short") + " " + countdown + (running ? "" : " · " + I18n.T("paused"));
                dirty = true;
            }
            if (!enabled) { Hide(); nextPosition = DateTime.MinValue; return; }
            if (DateTime.UtcNow >= nextPosition)
            {
                nextPosition = DateTime.UtcNow.AddSeconds(1);
                bool light = ReadLightMode(), contrast = SystemInformation.HighContrast;
                if (lightMode != light || highContrast != contrast) { lightMode = light; highContrast = contrast; dirty = true; }
                var taskbar = FindWindow("Shell_TrayWnd", null);
                if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out NativeRect bar))
                { taskbarBounds = Rectangle.Empty; Hide(); return; }
                taskbarBounds = bar.Rectangle;
                var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
                trayBounds = tray != IntPtr.Zero && GetWindowRect(tray, out NativeRect icons) ? icons.Rectangle : Rectangle.Empty;
                screenBounds = Screen.FromRectangle(taskbarBounds).Bounds;
                displayScale = GetScale(taskbar);
            }
            var target = Place(taskbarBounds, trayBounds, screenBounds, MeasureFrame(countdown, resting, displayScale), displayScale);
            if (target.IsEmpty || IsFullscreenWindow(GetForegroundWindow(), screenBounds)) { Hide(); return; }
            bool paint = dirty || !Visible || Size != target.Size;
            Bounds = target;
            if (paint && !PaintLayered(target)) { Hide(); return; }
            // Stay above ordinary windows without covering a foreground fullscreen application.
            SetWindowPos(Handle, new IntPtr(-1), target.X, target.Y, target.Width, target.Height, 0x0010 | 0x0040);
        }
        internal static bool CoversDisplay(Rectangle window, Rectangle display) => !display.IsEmpty
            && window.Left <= display.Left && window.Top <= display.Top
            && window.Right >= display.Right && window.Bottom >= display.Bottom;

        internal static bool IsFullscreenWindow(IntPtr foreground, Rectangle display)
        {
            if (foreground == IntPtr.Zero || !IsWindowVisible(foreground) || IsIconic(foreground)) return false;
            // The desktop and our own break reminders are not fullscreen games.
            var managed = Control.FromHandle(foreground);
            if (managed is BreakOverlay || NativeWindow.FromHandle(foreground) is TaskbarCountdown) return false;
            var name = new StringBuilder(128);
            GetClassName(foreground, name, name.Capacity);
            if (name.ToString() == "Progman" || name.ToString() == "WorkerW" || name.ToString() == "Shell_TrayWnd") return false;
            // DWM frame bounds can lag behind a resize. Check the current window
            // rectangle first so leaving fullscreen restores the timer immediately.
            if (!GetWindowRect(foreground, out NativeRect current) || !CoversDisplay(current.Rectangle, display)) return false;
            if (DwmGetWindowAttribute(foreground, 9, out NativeRect frame, Marshal.SizeOf(typeof(NativeRect))) != 0
                || frame.Rectangle.IsEmpty)
                frame = current;
            return CoversDisplay(frame.Rectangle, display);
        }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        private static float GetScale(IntPtr taskbar) { uint dpi = GetDpiForWindow(taskbar); return dpi == 0 ? 1 : dpi / 96f; }

        private static bool ReadLightMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key?.GetValue("SystemUsesLightTheme") is int light && light != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException) { return false; }
        }
        internal static Color ForegroundColor(bool lightMode, bool highContrast) => highContrast
            ? SystemColors.WindowText : lightMode ? Color.FromArgb(24, 24, 24) : Color.FromArgb(245, 245, 245);

        internal static Color BackgroundColor(bool lightMode, bool highContrast) => highContrast
            ? SystemColors.Window : lightMode ? Color.FromArgb(247, 250, 253) : Color.FromArgb(23, 34, 49);

        private static float TimeWidth(Graphics graphics, string time, Font font)
        {
            char[] digits = time.ToCharArray();
            for (int i = 0; i < digits.Length; i++) if (char.IsDigit(digits[i])) digits[i] = '8';
            return graphics.MeasureString(new string(digits), font, PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        internal static Size MeasureFrame(string time, bool isResting, float scale)
        {
            int height = (int)Math.Round(30 * scale);
            scale = height / 30f;
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var timeFont = new Font("Segoe UI", 12 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelFont = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float label = graphics.MeasureString(I18n.T(isResting ? "break_short" : "work_short"), labelFont,
                    PointF.Empty, StringFormat.GenericTypographic).Width;
                return new Size((int)Math.Ceiling(44 * scale + TimeWidth(graphics, time, timeFont) + label), height);
            }
        }

        internal static Bitmap RenderFrame(Size size, string time, bool isResting, bool isRunning, bool lightMode, bool highContrast)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            float scale = size.Height / 30f;
            using (var graphics = Graphics.FromImage(bitmap))
            using (var timeFont = new Font("Segoe UI", 12 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelFont = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(ForegroundColor(lightMode, highContrast)))
            using (var background = new SolidBrush(BackgroundColor(lightMode, highContrast)))
            using (var border = new Pen(highContrast ? SystemColors.WindowText : lightMode ? Color.FromArgb(205, 217, 229) : Color.FromArgb(53, 70, 91), Math.Max(1, scale)))
            using (var accent = new SolidBrush(highContrast ? SystemColors.WindowText : isResting
                ? (lightMode ? Color.FromArgb(96, 76, 174) : Color.FromArgb(193, 178, 255))
                : (lightMode ? Color.FromArgb(17, 112, 91) : Color.FromArgb(131, 230, 196))))
            using (var timeFormat = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (var captionFormat = new StringFormat(StringFormat.GenericTypographic) { Alignment = I18n.IsRtl ? StringAlignment.Near : StringAlignment.Far,
                LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap | (I18n.IsRtl ? StringFormatFlags.DirectionRightToLeft : 0) })
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var card = new RectangleF(1.5f * scale, 1.5f * scale, size.Width - 3 * scale, size.Height - 4 * scale);
                using (var path = Theme.RoundedRectangle(card, 8 * scale))
                {
                    graphics.FillPath(background, path);
                    graphics.DrawPath(border, path);
                }
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float timeWidth = TimeWidth(graphics, time, timeFont), divider = 12 * scale + timeWidth + 9 * scale;
                graphics.DrawString(time, timeFont, brush, new RectangleF(12 * scale, 0, timeWidth + scale, size.Height - scale), timeFormat);
                graphics.DrawString(I18n.T(isResting ? "break_short" : "work_short"), labelFont, accent,
                    new RectangleF(divider + 9 * scale, 0, size.Width - divider - 21 * scale, size.Height - scale), captionFormat);
                if (!isRunning)
                {
                    graphics.FillRectangle(accent, divider - 2 * scale, 11 * scale, scale, 7 * scale);
                    graphics.FillRectangle(accent, divider + scale, 11 * scale, scale, 7 * scale);
                }
                else graphics.DrawLine(border, divider, 10 * scale, divider, 19 * scale);
            }
            return bitmap;
        }

        private bool PaintLayered(Rectangle target)
        {
            using (var bitmap = RenderFrame(target.Size, countdown, resting, running, lightMode, highContrast))
            {
                IntPtr screenDC = GetDC(IntPtr.Zero), memoryDC = IntPtr.Zero, nativeBitmap = IntPtr.Zero, previous = IntPtr.Zero;
                try
                {
                    if (screenDC == IntPtr.Zero) return false;
                    memoryDC = CreateCompatibleDC(screenDC);
                    if (memoryDC == IntPtr.Zero) return false;
                    nativeBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                    previous = SelectObject(memoryDC, nativeBitmap);
                    var destination = new NativePoint(target.X, target.Y);
                    var size = new NativeSize(target.Width, target.Height);
                    var source = new NativePoint(0, 0);
                    var blend = new Blend { Alpha = 255, Format = 1 };
                    FrameUploaded = UpdateLayeredWindow(Handle, screenDC, ref destination, ref size, memoryDC, ref source, 0, ref blend, 2);
                    if (FrameUploaded) dirty = false;
                    return FrameUploaded;
                }
                finally
                {
                    if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memoryDC, previous);
                    if (nativeBitmap != IntPtr.Zero) DeleteObject(nativeBitmap);
                    if (memoryDC != IntPtr.Zero) DeleteDC(memoryDC);
                    if (screenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDC);
                }
            }
        }

    }
}
