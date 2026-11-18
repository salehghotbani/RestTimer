using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RestTimer
{
    internal static class UiIcons
    {
        public static string ForText(string name, string text)
        {
            if (text == "PNG") return "image";
            if (text == "PDF") return "document";
            string[] keys = { "start", "stop", "reset_timer", "save", "save_changes", "discard_changes", "skip", "start_break", "break_short", "install", "check_again", "updates_tab", "updates_menu", "dashboard", "general", "version_history", "about", "quit_app", "new_session", "project_site" };
            string[] icons = { "play", "pause", "reset", "save", "save", "close", "skip", "break", "break", "download", "reset", "download", "download", "dashboard", "settings", "history", "info", "quit", "play", "git" };
            for (int i=0;i<keys.Length;i++) if(name == "i18n:"+keys[i] || text == I18n.T(keys[i])) return icons[i];
            return null;
        }
        public static void Draw(Graphics graphics, Rectangle bounds, string icon, Color color)
        {
            var state=graphics.Save();
            graphics.TranslateTransform(bounds.Left,bounds.Top);graphics.ScaleTransform(bounds.Width/24f,bounds.Height/24f);
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var pen=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
            using(var brush=new SolidBrush(color))
            {
                Action<float,float,float,float> line=(a,b,c,d)=>graphics.DrawLine(pen,a,b,c,d);
                Action<float,float,float,float> rect=(a,b,c,d)=>graphics.DrawRectangle(pen,a,b,c,d);
                switch(icon)
                {
                    case "menu": line(4,6,20,6);line(4,12,20,12);line(4,18,20,18);break;
                    case "play": graphics.FillPolygon(brush,new[]{new PointF(8,5),new PointF(20,12),new PointF(8,19)});break;
                    case "pause": graphics.FillRectangle(brush,6,5,4,14);graphics.FillRectangle(brush,14,5,4,14);break;
                    case "reset": graphics.DrawArc(pen,4,4,16,16,215,300);line(3,3,3,9);line(3,9,9,9);break;
                    case "save": rect(4,3,16,18);rect(8,3,8,6);rect(8,14,8,7);break;
                    case "close": line(6,6,18,18);line(18,6,6,18);break;
                    case "skip": graphics.FillPolygon(brush,new[]{new PointF(5,5),new PointF(16,12),new PointF(5,19)});line(19,5,19,19);break;
                    case "break": line(7,2,7,5);line(12,2,12,5);rect(4,8,13,10);graphics.DrawArc(pen,14,8,7,7,270,180);line(3,21,19,21);break;
                    case "download": line(12,3,12,15);line(7,10,12,15);line(17,10,12,15);line(4,16,4,21);line(4,21,20,21);line(20,21,20,16);break;
                    case "dashboard": rect(3,3,7,7);rect(14,3,7,7);rect(3,14,7,7);rect(14,14,7,7);break;
                    case "settings": graphics.DrawEllipse(pen,5,5,14,14);graphics.DrawEllipse(pen,9,9,6,6);for(int i=0;i<8;i++){double a=i*Math.PI/4;line(12+(float)Math.Cos(a)*7,12+(float)Math.Sin(a)*7,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}break;
                    case "history": graphics.DrawEllipse(pen,3,3,18,18);line(12,7,12,12);line(12,12,16,15);break;
                    case "info": graphics.DrawEllipse(pen,3,3,18,18);graphics.FillEllipse(brush,11,6,2,2);line(12,11,12,17);break;
                    case "quit": line(10,3,4,3);line(4,3,4,21);line(4,21,10,21);line(9,12,21,12);line(16,7,21,12);line(16,17,21,12);break;
                    case "image": rect(3,4,18,16);graphics.DrawEllipse(pen,7,7,3,3);line(4,18,11,12);line(11,12,15,16);line(15,16,19,12);break;
                    case "document": rect(5,3,14,18);line(9,9,15,9);line(9,13,15,13);line(9,17,15,17);break;
                }
            }
            graphics.Restore(state);
        }
    }
}
