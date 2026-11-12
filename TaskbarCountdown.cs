using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RestTimer
{
    // A separate, non-activating window: Explorer's taskbar is never modified.
    internal sealed class TaskbarCountdown : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
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

        public TaskbarCountdown()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Surface;
            Opacity = 0.99; // Layered + WS_EX_TRANSPARENT passes clicks to windows in other processes.
            TopMost = true;
            DoubleBuffered = true;
            AccessibleName = "شمارش معکوس RestTimer";
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 2 || Height < 2) return;
            using (var path = Theme.RoundedRectangle(new RectangleF(0, 0, Width, Height), 7 * Height / 36f))
            {
                var previous = Region;
                Region = new Region(path);
                previous?.Dispose();
            }
        }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020; return value; }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; } // Click-through, including dragging.
            if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            base.WndProc(ref message);
        }

        internal static Rectangle Place(Rectangle taskbar, Rectangle tray, Rectangle screen, float scale)
        {
            int width = (int)Math.Round(112 * scale), height = (int)Math.Round(36 * scale);
            int gap = Math.Max(3, (int)Math.Round(5 * scale));
            // No guessed location for hidden, vertical, or unusually narrow taskbars.
            if (taskbar.Width <= taskbar.Height || taskbar.Height < height
                || Rectangle.Intersect(taskbar, screen).Height < taskbar.Height * 0.75
                || tray.Width <= 0 || tray.Height <= 0 || !taskbar.IntersectsWith(tray)) return Rectangle.Empty;
            var result = new Rectangle(tray.Left - gap - width, taskbar.Top + (taskbar.Height - height) / 2, width, height);
            return screen.Contains(result) && taskbar.Contains(result) ? result : Rectangle.Empty;
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
                Invalidate();
            }
            if (!enabled) { Hide(); nextPosition = DateTime.MinValue; return; }
            if (DateTime.UtcNow < nextPosition) return;
            nextPosition = DateTime.UtcNow.AddSeconds(1);
            var taskbar = FindWindow("Shell_TrayWnd", null);
            var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (taskbar == IntPtr.Zero || tray == IntPtr.Zero || !IsWindowVisible(taskbar)
                || !GetWindowRect(taskbar, out NativeRect bar) || !GetWindowRect(tray, out NativeRect icons)) { Hide(); return; }
            var monitor = Screen.FromRectangle(bar.Rectangle);
            float scale = monitor.Bounds.Height > 0 ? GetScale(taskbar) : 1;
            var target = Place(bar.Rectangle, icons.Rectangle, monitor.Bounds, scale);
            if (target.IsEmpty || CoveredByFullscreen(monitor.Bounds, taskbar)) { Hide(); return; }
            Bounds = target;
            if (!Visible) Show();
            SetWindowPos(Handle, new IntPtr(-1), target.X, target.Y, target.Width, target.Height, 0x0010 | 0x0040);
        }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        private static float GetScale(IntPtr taskbar) { uint dpi = GetDpiForWindow(taskbar); return dpi == 0 ? 1 : dpi / 96f; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Height / 36f;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var accent = !running ? Theme.Muted : resting ? Color.FromArgb(255, 204, 132) : Theme.Mint;
            using (var path = Theme.RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 7 * scale))
            using (var background = new SolidBrush(Theme.Surface))
            using (var border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(background, path);
                e.Graphics.DrawPath(border, path);
            }
            using (var captionFont = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var timeFont = new Font("Segoe UI", 14 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(e.Graphics, (resting ? "استراحت" : "کار") + (running ? "" : " · مکث"), captionFont,
                    new Rectangle(0, 0, Width, Height / 2), accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, countdown, timeFont, new Rectangle(0, Height / 2 - 1, Width, Height / 2), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }
}
