using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class StyledComboBox : ComboBox
    {
        private readonly ToolTip hint=new ToolTip();
        protected override void OnTextChanged(EventArgs e) {base.OnTextChanged(e);hint?.SetToolTip(this,Text);}
        protected override void Dispose(bool disposing) {base.Dispose(disposing);if(disposing)hint?.Dispose();}
        public StyledComboBox()
        {
            DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;
            DrawMode=DrawMode.OwnerDrawFixed;IntegralHeight=false;DropDownHeight=320;
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e);ItemHeight=Font.Height+8; }
        protected override void OnDropDown(EventArgs e)
        {
            int width=Width;foreach(var item in Items) width=Math.Max(width,TextRenderer.MeasureText(item.ToString(),Font).Width+40);
            DropDownWidth=Math.Min(width,Screen.FromControl(this).WorkingArea.Width-20);base.OnDropDown(e);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if(e.Index<0)return;
            bool active=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(active?Theme.Selection:Theme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,Items[e.Index].ToString(),Font,Rectangle.Inflate(e.Bounds,-10,0),active?Theme.OnSelection:Theme.Text,
                TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|(RightToLeft==RightToLeft.Yes?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left));
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if((m.Msg!=0x000F && m.Msg!=0x0317 && m.Msg!=0x0318) || Width<3 || Height<3)return;
            using(var g=m.Msg==0x000F?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam))
            {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                Color background=Theme.Background;
                for(Control parent=Parent;parent!=null;parent=parent.Parent) {if(parent is RoundedPanel panel){background=panel.FillColor;break;}if(parent.BackColor.A==255){background=parent.BackColor;break;}}
                using(var fill=new SolidBrush(background))g.FillRectangle(fill,ClientRectangle);
                using(var path=Theme.RoundedRectangle(new RectangleF(.5f,.5f,Width-1,Height-1),7*Theme.ScaleFor(this)))
                using(var fill=new SolidBrush(Theme.Input))using(var pen=new Pen(Focused?Theme.Mint:Theme.Border)) {g.FillPath(fill,path);g.DrawPath(pen,path);}
                bool rtl=RightToLeft==RightToLeft.Yes;int arrow=rtl?17:Width-17;
                using(var pen=new Pen(Enabled?Theme.Muted:Theme.Border,1.6f))g.DrawLines(pen,new[]{new Point(arrow-4,Height/2-2),new Point(arrow,Height/2+2),new Point(arrow+4,Height/2-2)});
                var bounds=new Rectangle(rtl?32:10,0,Math.Max(1,Width-42),Height);
                TextRenderer.DrawText(g,Text,Font,bounds,Enabled?Theme.Text:Theme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|(rtl?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left));
            }
        }
    }
}
