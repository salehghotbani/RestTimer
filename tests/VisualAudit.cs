using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class VisualAudit
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    private static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> issues=new List<string>();
    private static string sample;
    private static void Inspect(Control parent, string path)
    {
        var list=parent as ListView;
        if(list!=null && list.Visible && list.Columns.Cast<ColumnHeader>().Sum(c=>c.Width)>list.ClientSize.Width)
            issues.Add(sample+" | table | columns exceed viewport: "+list.Columns.Cast<ColumnHeader>().Sum(c=>c.Width)+" > "+list.ClientSize.Width);
        if(!parent.Visible || parent.GetType().Name=="ScrollFrame" || parent is TextBoxBase || parent is ComboBox || parent is ListView) return;
        var visible=parent.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
        for(int a=0;a<visible.Length;a++) for(int b=a+1;b<visible.Length;b++)
        {
            var overlap=Rectangle.Intersect(visible[a].Bounds,visible[b].Bounds);
            if(overlap.Width>2 && overlap.Height>2)
                issues.Add(sample+" | overlap | "+path+" "+visible[a].GetType().Name+"/"+visible[b].GetType().Name+" "+overlap);
        }
        foreach(Control child in parent.Controls)
        {
            if(!child.Visible) continue;
            string name=path+"/"+(string.IsNullOrEmpty(child.Name)?child.GetType().Name:child.Name);
            if(child.Left < -2 || child.Top < -2 || child.Right>parent.ClientSize.Width+2 || child.Bottom>parent.ClientSize.Height+2)
                issues.Add(sample+" | overflow | "+name+" "+child.Bounds+" parent="+parent.ClientSize);
            var label=child as Label;
            if(label!=null && !label.AutoSize && !string.IsNullOrWhiteSpace(label.Text))
            {
                int required=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(1,label.Width),32767),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
                if(required>label.Height+2) issues.Add(sample+" | text | "+name+" needs="+required+" has="+label.Height+" text="+label.Text.Replace('\n',' '));
            }
            Inspect(child,name);
        }
    }
    private static void Pump(int milliseconds)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        do {Application.DoEvents();System.Threading.Thread.Sleep(5);} while(clock.ElapsedMilliseconds<milliseconds);
    }
    private static void Popups(Assembly assembly,string output,string code,string mode,Icon icon)
    {
        int index=0;
        using(var sheet=new Bitmap(1600,420)) using(var canvas=Graphics.FromImage(sheet))
        {
            canvas.Clear(Color.Gray);
            foreach(string kind in new[]{"UnsavedChangesDialog","WorkStartDialog"})
            using(var form=(Form)Activator.CreateInstance(assembly.GetType("RestTimer."+kind),kind=="WorkStartDialog"?new object[]{3600,"A report"}:new object[]{icon}))
            {
                form.Show();Pump(10);sample=code+" "+mode+" "+kind;Inspect(form,kind);
                using(var frame=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(frame,new Rectangle(Point.Empty,form.Size));canvas.DrawImage(frame,new Rectangle(index*540,0,520,400));}
                index++;form.Dispose();
            }
            using(var overlay=(Form)Activator.CreateInstance(assembly.GetType("RestTimer.BreakOverlay"),new object[]{Screen.PrimaryScreen}))
            {
                overlay.GetType().GetMethod("SetCountdown").Invoke(overlay,new object[]{"14:52"});overlay.GetType().GetMethod("SetRunning").Invoke(overlay,new object[]{false});
                overlay.Show();Pump(5);var card=(Control)overlay.GetType().GetField("card",Fields).GetValue(overlay);
                sample=code+" "+mode+" break";Inspect(card,"break");
                using(var frame=new Bitmap(card.Width,card.Height)) {card.DrawToBitmap(frame,card.ClientRectangle);canvas.DrawImage(frame,new Rectangle(1080,0,520,130));}
                overlay.Dispose();
            }
            sheet.Save(output+"/popups-"+code+"-"+mode+".png");
        }
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();
            var assembly=Assembly.LoadFrom(Path.GetFullPath("dist/Windows/RestTimer.exe"));
            var settings=assembly.GetType("RestTimer.SettingsDialog");var prefs=assembly.GetType("RestTimer.Preferences");
            var i18n=assembly.GetType("RestTimer.I18n");var theme=assembly.GetType("RestTimer.Theme");
            string output="obj/visual-audit";Directory.CreateDirectory(output);
            var codes=args.Length>0?args:((string[])i18n.GetField("SupportedCodes").GetValue(null));
            foreach(string code in codes) foreach(string mode in new[]{"light","dark"})
            {
                i18n.GetMethod("Use").Invoke(null,new object[]{code});theme.GetMethod("Use").Invoke(null,new object[]{mode});
                var preferences=Activator.CreateInstance(prefs,new object[]{3600,900,true,code,"auto",mode});
                var updater=Activator.CreateInstance(assembly.GetType("RestTimer.UpdateService"),new object[]{Path.GetFullPath(output+"/updates")});
                using(var icon=Icon.ExtractAssociatedIcon(Path.GetFullPath("dist/Windows/RestTimer.exe")))
                using(var dialog=(Form)Activator.CreateInstance(settings,preferences,icon,(Action)(()=>{}),(Action)(()=>{}),updater,"general",false))
                using(var sheet=new Bitmap(1500,1040))
                using(var canvas=Graphics.FromImage(sheet))
                {
                    canvas.Clear(Color.Gray);dialog.Show();Pump(10);settings.GetMethod("SetRunning").Invoke(dialog,new object[]{false});
                    var report=settings.GetField("reportPage",Fields).GetValue(dialog);var day=report.GetType().GetField("day",Fields).GetValue(report);
                    day.GetType().GetProperty("Value").SetValue(day,new DateTime(2000,1,1));
                    int row=0;
                    foreach(var size in new[]{new Size(960,780),new Size(780,620)}) foreach(bool collapsed in new[]{false,true})
                    {
                        // Exercise outer-window resizing, matching desktop interaction.
                        dialog.Size=size.Width==780?new Size(780,640):new Size(980,820);
                        settings.GetMethod("SetSidebarCollapsed").Invoke(dialog,new object[]{collapsed});
                        int col=0;
                        foreach(string page in new[]{"report","general","updates","history","about"})
                        {
                            settings.GetMethod("SelectPage").Invoke(dialog,new object[]{page});Pump(20);
                            sample=code+" "+mode+" "+size+" "+(collapsed?"rail":"expanded")+" "+page;
                            Inspect(dialog,"Settings");
                            using(var frame=new Bitmap(dialog.Width,dialog.Height))
                            {dialog.DrawToBitmap(frame,new Rectangle(Point.Empty,dialog.Size));canvas.DrawImage(frame,new Rectangle(col*300,row*260,300,240));}
                            using(var font=new Font("Segoe UI",8)) canvas.DrawString(page+" "+size.Width+" "+(collapsed?"rail":"open"),font,Brushes.White,col*300,row*260+240);
                            col++;
                        }
                        row++;
                    }
                    sheet.Save(output+"/"+code+"-"+mode+".png",ImageFormat.Png);
                    settings.GetMethod("SetSidebarCollapsed").Invoke(dialog,new object[]{false});
                    var nav=(System.Collections.IDictionary)settings.GetField("navigation",Fields).GetValue(dialog);
                    int top=((Control)nav["report"]).PointToScreen(Point.Empty).Y;
                    var toggle=(Button)settings.GetField("sidebarToggle",Fields).GetValue(dialog);
                    var duration=settings.GetField("workDuration",Fields).GetValue(dialog);
                    var seconds=(TextBox)duration.GetType().GetField("seconds",Fields).GetValue(duration);seconds.Text="17";
                    float scale=dialog.AutoScaleDimensions.Width/96f;
                    Func<int> anchor=()=> { var button=(Control)nav["report"];return button.PointToScreen(new Point(button.RightToLeft==RightToLeft.Yes?button.Width-(int)(21*scale):(int)(21*scale),0)).X; };
                    int iconX=anchor(),lastWidth=((Control)settings.GetField("sidebar",Fields).GetValue(dialog)).Width;
                    toggle.PerformClick();
                    for(int step=0;step<15;step++)
                    {
                        Pump(16);var side=(Control)settings.GetField("sidebar",Fields).GetValue(dialog);
                        if(side.Width>lastWidth+1 || Math.Abs(anchor()-iconX)>2 || ((Control)nav["report"]).PointToScreen(Point.Empty).Y!=top)
                            issues.Add(code+" "+mode+" | sidebar | unstable animation frame");
                        lastWidth=side.Width;
                    }
                    if(((Control)nav["report"]).PointToScreen(Point.Empty).Y!=top) issues.Add(code+" "+mode+" | sidebar | navigation jumps vertically during collapse");
                    toggle.PerformClick();Pump(45);toggle.PerformClick();Pump(45);toggle.PerformClick();Pump(240);
                    if((bool)settings.GetProperty("SidebarCollapsed").GetValue(dialog) || seconds.Text!="17" || (string)settings.GetProperty("CurrentPage").GetValue(dialog)!="about")
                        issues.Add(code+" "+mode+" | sidebar | interrupted animation lost state");
                    dialog.Dispose();
                    Popups(assembly,output,code,mode,icon);
                }
                Console.WriteLine("Audited "+code+" "+mode);
                File.WriteAllLines(output+"/issues.txt",issues);
            }
            File.WriteAllLines(output+"/issues.txt",issues);
            Console.WriteLine("Visual audit findings: "+issues.Count);
            return issues.Count==0?0:2;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
