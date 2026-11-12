using System;
using System.Drawing;
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
    internal sealed class TaskbarCountdown : Form
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
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        private string countdown = "1:00:00";
        private bool resting, running = true;
        private DateTime nextPosition;
        private Color textColor = Color.White;
        private bool dirty = true;
        private readonly AppbarReservation reservation = new AppbarReservation();
        internal bool FrameUploaded { get; private set; }
        internal Rectangle ReservedBounds => reservation.Bounds;
        internal bool HasReservation => reservation.Registered;

        public TaskbarCountdown()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            DoubleBuffered = true;
            AccessibleName = "شمارش معکوس RestTimer";
            reservation.LayoutChanged += () => nextPosition = DateTime.MinValue;
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020 | 0x00080000; return value; }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; } // Click-through, including dragging.
            if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (message.Msg == 0x001A || message.Msg == 0x031A) { dirty = true; nextPosition = DateTime.MinValue; }
            base.WndProc(ref message);
        }

        internal static Rectangle Place(Rectangle reservedStrip, Rectangle tray, Rectangle screen, float scale)
        {
            int width = (int)Math.Round(100 * scale), height = (int)Math.Round(20 * scale);
            int gap = Math.Max(3, (int)Math.Round(5 * scale));
            if (reservedStrip.Width <= reservedStrip.Height || reservedStrip.Height < height || tray.Width <= 0
                || tray.Height <= 0 || !screen.Contains(reservedStrip)) return Rectangle.Empty;
            int x = Math.Max(reservedStrip.Left, Math.Min(tray.Left - gap - width, reservedStrip.Right - width));
            var result = new Rectangle(x, reservedStrip.Top + (reservedStrip.Height - height) / 2, width, height);
            return screen.Contains(result) && reservedStrip.Contains(result) ? result : Rectangle.Empty;
        }

        private bool CoveredByFullscreen(Rectangle screen, IntPtr taskbar)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == taskbar) return false;
            GetWindowThreadProcessId(foreground, out uint process);
            if (process == GetCurrentProcessId()) return false;
            var name = new StringBuilder(64);
            GetClassName(foreground, name, name.Capacity);
            if (name.ToString() == "Progman" || name.ToString() == "WorkerW") return false;
            return GetWindowRect(foreground, out NativeRect bounds) && bounds.Rectangle.Contains(screen);
        }

        public void UpdateDisplay(string text, bool isResting, bool isRunning, bool enabled)
        {
            if (countdown != text || resting != isResting || running != isRunning)
            {
                countdown = text; resting = isResting; running = isRunning;
                AccessibleDescription = (resting ? "استراحت " : "کار ") + countdown + (running ? "" : "، متوقف");
                dirty = true;
            }
            if (!enabled) { Hide(); reservation.Release(); nextPosition = DateTime.MinValue; return; }
            if (DateTime.UtcNow < nextPosition)
            {
                if (Visible && dirty && !PaintLayered(Bounds)) Hide();
                return;
            }
            nextPosition = DateTime.UtcNow.AddSeconds(1);
            var foreground = ForegroundColor(ReadLightMode(), SystemInformation.HighContrast);
            if (textColor != foreground) { textColor = foreground; dirty = true; }
            var taskbar = FindWindow("Shell_TrayWnd", null);
            var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (taskbar == IntPtr.Zero || tray == IntPtr.Zero || !IsWindowVisible(taskbar)
                || !GetWindowRect(taskbar, out NativeRect bar) || !GetWindowRect(tray, out NativeRect icons)) { Hide(); reservation.Release(); return; }
            var monitor = Screen.FromRectangle(bar.Rectangle);
            float scale = monitor.Bounds.Height > 0 ? GetScale(taskbar) : 1;
            if (bar.Rectangle.Width <= bar.Rectangle.Height || bar.Rectangle.Top < monitor.Bounds.Top + monitor.Bounds.Height / 2
                || !reservation.Reserve(monitor.Bounds, (int)Math.Ceiling(24 * scale)))
            { Hide(); reservation.Release(); return; }
            var target = Place(reservation.Bounds, icons.Rectangle, monitor.Bounds, scale);
            if (target.IsEmpty) { Hide(); reservation.Release(); return; }
            if (CoveredByFullscreen(monitor.Bounds, taskbar)) { Hide(); return; }
            bool paint = dirty || !Visible || Size != target.Size;
            Bounds = target;
            if (paint && !PaintLayered(target)) { Hide(); return; }
            if (!Visible) Show();
            SetWindowPos(Handle, new IntPtr(-1), target.X, target.Y, target.Width, target.Height, 0x0010 | 0x0040);
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

        internal static Bitmap RenderFrame(Size size, string time, bool isResting, bool isRunning, Color foreground)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            float scale = size.Height / 20f;
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(foreground))
            using (var timeFormat = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (var captionFormat = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft })
            {
                graphics.Clear(Color.Transparent);
                // Grayscale anti-aliasing preserves alpha without a colored background fringe.
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(time, font, brush, new RectangleF(2 * scale, 0, 53 * scale, size.Height), timeFormat);
                graphics.DrawString(isResting ? "استراحت" : "کار", font, brush,
                    new RectangleF(57 * scale, 0, size.Width - 59 * scale, size.Height), captionFormat);
                if (!isRunning)
                {
                    graphics.FillRectangle(brush, 53 * scale, 7 * scale, scale, 6 * scale);
                    graphics.FillRectangle(brush, 56 * scale, 7 * scale, scale, 6 * scale);
                }
            }
            return bitmap;
        }

        private bool PaintLayered(Rectangle target)
        {
            using (var bitmap = RenderFrame(target.Size, countdown, resting, running, textColor))
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) reservation.Dispose();
            base.Dispose(disposing);
        }
    }
}
