using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RestTimer
{
    // A Shell-managed desktop appbar. It reserves a separate horizontal strip;
    // it never changes Explorer's task button layout or the taskbar window.
    internal sealed class AppbarReservation : NativeWindow, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
            public Rect(Rectangle value) { Left = value.Left; Top = value.Top; Right = value.Right; Bottom = value.Bottom; }
            public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Data
        {
            public uint Size;
            public IntPtr Window;
            public uint Callback, Edge;
            public Rect Bounds;
            public IntPtr Parameter;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
        [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref Data data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref Rect bounds, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        private const uint CallbackMessage = 0x8000 + 120;
        private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        private bool registered, dirty = true, positioning, disposed;
        private Rectangle lastScreen;
        private int lastHeight;
        public Rectangle Bounds { get; private set; }
        public bool Registered => registered;
        public event Action LayoutChanged;

        private Data MessageData() => new Data { Size = (uint)Marshal.SizeOf(typeof(Data)), Window = Handle, Callback = CallbackMessage, Edge = 3 };

        public bool Reserve(Rectangle screen, int height)
        {
            if (disposed || height < 1 || screen.Width < 1 || height >= screen.Height) return false;
            if (Handle == IntPtr.Zero)
            {
                CreateHandle(new CreateParams { Caption = "RestTimer reserved countdown strip", Style = unchecked((int)0x80000000),
                    ExStyle = 0x08000000 | 0x00000080 | 0x00080000 | 0x00000020, Width = 1, Height = 1 });
                if (!SetLayeredWindowAttributes(Handle, 0, 0, 2)) { DestroyHandle(); return false; }
            }
            if (lastScreen != screen && registered) Release();
            var data = MessageData();
            if (!registered)
            {
                registered = SHAppBarMessage(0, ref data) != UIntPtr.Zero; // ABM_NEW
                if (!registered) return false;
                dirty = true;
            }
            if (!dirty && lastScreen == screen && lastHeight == height) return !Bounds.IsEmpty;
            positioning = true;
            dirty = false;
            try
            {
                data.Bounds = new Rect(new Rectangle(screen.Left, screen.Bottom - height, screen.Width, height));
                SHAppBarMessage(2, ref data); // ABM_QUERYPOS: Shell excludes this HWND's own reservation.
                data.Bounds.Top = data.Bounds.Bottom - height;
                var candidate = data.Bounds.Rectangle;
                if (!screen.Contains(candidate) || candidate.Width < 1) { Release(); return false; }
                if (candidate != Bounds)
                {
                    SHAppBarMessage(3, ref data); // ABM_SETPOS
                    candidate = data.Bounds.Rectangle;
                }
                if (candidate.Height < height || !screen.Contains(candidate)) { Release(); return false; }
                Bounds = candidate;
                lastScreen = screen;
                lastHeight = height;
                if (!SetWindowPos(Handle, new IntPtr(-1), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, 0x0010 | 0x0040))
                { Release(); return false; }
                return true;
            }
            finally { positioning = false; }
        }

        public void Release()
        {
            if (registered)
            {
                registered = false;
                var data = MessageData();
                SHAppBarMessage(1, ref data); // ABM_REMOVE restores the Shell-managed work area.
            }
            Bounds = Rectangle.Empty;
            dirty = true;
            if (Handle != IntPtr.Zero) ShowWindow(Handle, 0);
        }

        internal static Rectangle WorkArea(Rectangle screen)
        {
            var bounds = new Rect(screen);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf(typeof(MonitorInfo)) };
            var monitor = MonitorFromRect(ref bounds, 2);
            return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info) ? info.Work.Rectangle : Rectangle.Empty;
        }

        protected override void WndProc(ref Message message)
        {
            if ((uint)message.Msg == taskbarCreated)
            {
                registered = false; Bounds = Rectangle.Empty; dirty = true;
                LayoutChanged?.Invoke();
            }
            else if ((uint)message.Msg == CallbackMessage && (message.WParam.ToInt64() == 1 || message.WParam.ToInt64() == 0) && !positioning)
            {
                dirty = true;
                LayoutChanged?.Invoke();
            }
            else if (message.Msg == 0x0047 && registered) // WM_WINDOWPOSCHANGED
            {
                var data = MessageData();
                SHAppBarMessage(9, ref data); // ABM_WINDOWPOSCHANGED
            }
            else if (message.Msg == 0x0006 && registered) // WM_ACTIVATE
            {
                var data = MessageData();
                data.Parameter = message.WParam;
                SHAppBarMessage(6, ref data); // ABM_ACTIVATE
            }
            else if (message.Msg == 0x007E || message.Msg == 0x02E0) // Display or DPI change.
            {
                dirty = true;
                LayoutChanged?.Invoke();
            }
            else if (message.Msg == 0x0002) Release(); // WM_DESTROY
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Release();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
