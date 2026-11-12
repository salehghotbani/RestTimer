using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RestTimer
{
    // Clip only the native scrollbar; the editor keeps its native wheel, keyboard,
    // selection and accessibility behavior. The visible thumb follows its range.
    internal sealed class ScrollFrame : UserControl
    {
        [StructLayout(LayoutKind.Sequential)] private struct ScrollInfo
        { public uint Size, Mask; public int Minimum, Maximum; public uint Page; public int Position, TrackPosition; }
        [StructLayout(LayoutKind.Sequential)] private struct ScrollPoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr window, int bar, ref ScrollInfo info);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref ScrollPoint point);
        private readonly Control editor;
        private readonly Panel viewport;
        private readonly SlimScrollBar scrollbar;
        private readonly Timer refresh;
        private bool syncing;
        public void ApplyDirection()
        {
            RightToLeft = I18n.Flow;
            scrollbar.Dock = I18n.IsRtl ? DockStyle.Left : DockStyle.Right;
            Synchronize();
        }

        public ScrollFrame(Control editor)
        {
            this.editor = editor;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            editor.Dock = DockStyle.None;
            // Paragraph direction in release notes is set in their RTF, independently
            // of scrollbar placement. Keep the clipped native bar on the right.
            editor.RightToLeft = RightToLeft.No;
            viewport = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, RightToLeft = RightToLeft.No };
            scrollbar = new SlimScrollBar { Dock = I18n.IsRtl ? DockStyle.Left : DockStyle.Right,
                Visible = false, AccessibleName = editor.AccessibleName };
            viewport.Controls.Add(editor);
            Controls.Add(viewport);
            Controls.Add(scrollbar);
            viewport.Resize += (_, __) => Synchronize();
            editor.Layout += (_, __) => Synchronize();
            editor.TextChanged += (_, __) => Synchronize();
            if (editor is RichTextBox text) text.VScroll += (_, __) => Synchronize();
            if (editor is ScrollableControl panel) panel.Scroll += (_, __) => Synchronize();
            if (editor is ListBox list) list.SelectedIndexChanged += (_, __) => Synchronize();
            scrollbar.ValueChanged += ScrollEditor;
            refresh = new Timer { Interval = 120 };
            refresh.Tick += (_, __) => { if (Visible) Synchronize(); };
            refresh.Start();
        }

        private void Synchronize()
        {
            if (syncing || IsDisposed || editor.IsDisposed || !editor.IsHandleCreated) return;
            syncing = true;
            try
            {
                bool nativeBar = (GetWindowLong(editor.Handle, -16) & 0x00200000) != 0;
                editor.Bounds = new Rectangle(0, 0, viewport.ClientSize.Width + (nativeBar ? SystemInformation.VerticalScrollBarWidth : 0), viewport.ClientSize.Height);
                var info = new ScrollInfo { Size = (uint)Marshal.SizeOf(typeof(ScrollInfo)), Mask = 0x17 };
                bool hasRange = GetScrollInfo(editor.Handle, 1, ref info);
                int maximum = hasRange ? Math.Max(info.Minimum, info.Maximum - (int)info.Page + 1) : 0;
                bool needed = nativeBar && hasRange && info.Page > 0 && maximum > info.Minimum;
                scrollbar.Visible = needed;
                scrollbar.SetRange(info.Minimum, maximum, (int)info.Page, info.Position);
                // Showing the custom bar changes the viewport width and can reflow text.
                nativeBar = (GetWindowLong(editor.Handle, -16) & 0x00200000) != 0;
                editor.Bounds = new Rectangle(0, 0, viewport.ClientSize.Width + (nativeBar ? SystemInformation.VerticalScrollBarWidth : 0), viewport.ClientSize.Height);
            }
            finally { syncing = false; }
        }

        private void ScrollEditor(int value)
        {
            if (editor is RichTextBox)
            {
                var point = new ScrollPoint();
                SendMessage(editor.Handle, 0x04DD, IntPtr.Zero, ref point);
                point.Y = value;
                SendMessage(editor.Handle, 0x04DE, IntPtr.Zero, ref point);
            }
            else if (editor is ListBox list) list.TopIndex = value;
            else if (editor is ScrollableControl panel) panel.AutoScrollPosition = new Point(-panel.AutoScrollPosition.X, value);
            Synchronize();
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); Synchronize(); }
        protected override void Dispose(bool disposing) { if (disposing) refresh?.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class SlimScrollBar : Control
    {
        private int minimum, maximum, page, value, dragOffset;
        private bool hovered, dragging;
        public event Action<int> ValueChanged;
        public SlimScrollBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Width = 14;
            TabStop = false;
            AccessibleRole = AccessibleRole.ScrollBar;
        }
        public void SetRange(int minimum, int maximum, int page, int value)
        {
            value = Math.Max(minimum, Math.Min(maximum, value));
            if (this.minimum == minimum && this.maximum == maximum && this.page == page && this.value == value) return;
            this.minimum = minimum; this.maximum = maximum; this.page = page; this.value = value;
            Invalidate();
        }
        private Rectangle Thumb
        {
            get
            {
                int track = Math.Max(1, Height - 8);
                int height = Math.Min(track, Math.Max((int)(24 * DeviceDpi / 96f), (int)((long)track * page / Math.Max(1, maximum - minimum + page))));
                int top = 4 + (int)((long)(track - height) * (value - minimum) / Math.Max(1, maximum - minimum));
                int width = Math.Max(4, (int)(6 * DeviceDpi / 96f));
                return new Rectangle((Width - width) / 2, top, width, height);
            }
        }
        private void ChangeValue(int next)
        {
            next = Math.Max(minimum, Math.Min(maximum, next));
            if (next == value) return;
            value = next; Invalidate(); ValueChanged?.Invoke(value);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (maximum <= minimum) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var thumb = Thumb;
            using (var path = Theme.RoundedRectangle(thumb, thumb.Width / 2f))
            using (var fill = new SolidBrush(hovered || dragging ? Theme.Mint : Theme.Muted)) e.Graphics.FillPath(fill, path);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (Thumb.Contains(e.Location)) { dragging = true; dragOffset = e.Y - Thumb.Top; Capture = true; }
            else ChangeValue(value + (e.Y < Thumb.Top ? -1 : 1) * Math.Max(1, page));
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int travel = Height - 8 - Thumb.Height;
            if (travel > 0) ChangeValue(minimum + (int)((long)(e.Y - dragOffset - 4) * (maximum - minimum) / travel));
        }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragging = false; base.OnMouseCaptureChanged(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0 ? page : Math.Max(1, lines) * Math.Max(1, page / 12);
            ChangeValue(value - e.Delta / 120 * step);
            base.OnMouseWheel(e);
        }
        protected override bool IsInputKey(Keys key) => key == Keys.Up || key == Keys.Down || key == Keys.PageUp
            || key == Keys.PageDown || key == Keys.Home || key == Keys.End || base.IsInputKey(key);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up: ChangeValue(value - 1); break;
                case Keys.Down: ChangeValue(value + 1); break;
                case Keys.PageUp: ChangeValue(value - page); break;
                case Keys.PageDown: ChangeValue(value + page); break;
                case Keys.Home: ChangeValue(minimum); break;
                case Keys.End: ChangeValue(maximum); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
        }
    }
}
