using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class ResizeTransition : Control
    {
        private Bitmap before,after;
        public float Progress;
        public ResizeTransition(Bitmap before,Bitmap after)
        {
            this.before=before;this.after=after;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);TabStop=false;
        }
        private static Bitmap Canvas(Control control)
        {
            var bitmap=new Bitmap(Math.Max(1,control.ClientSize.Width),Math.Max(1,control.ClientSize.Height));
            bitmap.SetResolution(control.DeviceDpi,control.DeviceDpi);return bitmap;
        }
        public static Bitmap Snapshot(Control control)
        {
            var image=Canvas(control);
            control.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            PaintNotes(control,control,image);return image;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct Range { public int First,Last; }
        [StructLayout(LayoutKind.Sequential)] private struct Format { public IntPtr Hdc,Target;public Rect Area,Page;public Range Characters; }
        [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr flag,IntPtr data);
        private static void PaintNotes(Control root,Control parent,Bitmap target)
        {
            foreach(Control child in parent.Controls)
            {
                if(!child.Visible)continue;
                var notes=child as RichTextBox;
                if(notes==null) { PaintNotes(root,child,target);continue; }
                var origin=root.PointToClient(notes.PointToScreen(Point.Empty));
                var clip=new Rectangle(origin,notes.ClientSize);
                for(Control ancestor=notes.Parent;ancestor!=root && ancestor!=null;ancestor=ancestor.Parent)
                    clip.Intersect(new Rectangle(root.PointToClient(ancestor.PointToScreen(Point.Empty)),ancestor.ClientSize));
                using(var bitmap=Canvas(notes))
                using(var g=Graphics.FromImage(bitmap))
                {
                    g.Clear(notes.BackColor);
                    var area=new Rect { Right=(int)(bitmap.Width*1440/g.DpiX),Bottom=(int)(bitmap.Height*1440/g.DpiY) };
                    IntPtr hdc=g.GetHdc(),memory=Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Format)));
                    try
                    {
                        int line=SendMessage(notes.Handle,0x00CE,IntPtr.Zero,IntPtr.Zero).ToInt32();
                        var format=new Format {Hdc=hdc,Target=hdc,Area=area,Page=area,Characters=new Range {First=Math.Max(0,notes.GetFirstCharIndexFromLine(line)),Last=-1}};
                        Marshal.StructureToPtr(format,memory,false);SendMessage(notes.Handle,0x0439,new IntPtr(1),memory);
                    }
                    finally {SendMessage(notes.Handle,0x0439,IntPtr.Zero,IntPtr.Zero);Marshal.FreeCoTaskMem(memory);g.ReleaseHdc(hdc);}
                    using(var output=Graphics.FromImage(target)) { output.SetClip(clip);output.DrawImageUnscaled(bitmap,origin); }
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            Draw(e.Graphics,before,1);Draw(e.Graphics,after,Progress);
        }
        private void Draw(Graphics g,Bitmap bitmap,float alpha)
        {
            using(var attributes=new ImageAttributes())
            {
                var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);
                g.DrawImage(bitmap,ClientRectangle,0,0,bitmap.Width,bitmap.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing){before.Dispose();after.Dispose();}base.Dispose(disposing); }
    }
}
