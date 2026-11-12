using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new MenuColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.ToolStrip.DeviceDpi / 96f;
            var bounds = new RectangleF(2 * scale, scale, e.Item.Width - 4 * scale, e.Item.Height - 2 * scale);
            using (var path = Theme.RoundedRectangle(bounds, 4 * scale))
            using (var fill = new SolidBrush(Theme.Selection))
                e.Graphics.FillPath(fill, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? (e.Item.Selected ? Theme.OnSelection : Theme.Text) : Theme.Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.Border))
            {
                float inset = 6 * e.ToolStrip.DeviceDpi / 96f;
                e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, e.Item.Width - inset, e.Item.Height / 2);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(e.Item.Selected ? Theme.OnSelection : Theme.AccentText, 2))
                e.Graphics.DrawLines(pen, new[]
                {
                    new Point(bounds.Left + 2, bounds.Top + bounds.Height / 2),
                    new Point(bounds.Left + bounds.Width / 3, bounds.Bottom - 3),
                    new Point(bounds.Right - 1, bounds.Top + 2)
                });
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Theme.Surface;
            public override Color MenuBorder => Theme.Border;
            public override Color ImageMarginGradientBegin => Theme.Surface;
            public override Color ImageMarginGradientMiddle => Theme.Surface;
            public override Color ImageMarginGradientEnd => Theme.Surface;
        }
    }
}
