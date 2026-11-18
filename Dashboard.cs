using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class DashboardChart : Control
    {
        public WorkEntry[] Entries = new WorkEntry[0];
        public int Mode;
        private static readonly Color[] Colors = { Color.FromArgb(24,156,129), Color.FromArgb(66,133,224), Color.FromArgb(227,160,58), Color.FromArgb(160,110,222), Color.FromArgb(218,100,123), Color.FromArgb(49,158,187), Color.FromArgb(153,166,57), Color.FromArgb(131,142,159) };
        public DashboardChart() { DoubleBuffered = true; Dock = DockStyle.Fill; MinimumSize = new Size(280,220); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Draw(e.Graphics, ClientRectangle, Entries, Mode, false); }
        internal static void DrawText(Graphics g, string text, Font font, Color color, RectangleF box, bool right = false, bool ltr = false)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = right != (I18n.IsRtl && !ltr) ? StringAlignment.Far : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            { if (I18n.IsRtl && !ltr) format.FormatFlags |= StringFormatFlags.DirectionRightToLeft; g.DrawString(text, font, brush, box, format); }
        }
        public static void Draw(Graphics g, Rectangle area, WorkEntry[] raw, int mode, bool print)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color ink = print ? Color.FromArgb(26,40,56) : Theme.Text, muted = print ? Color.FromArgb(83,103,120) : Theme.Muted;
            using (var back = new SolidBrush(print ? Color.White : Theme.Surface)) g.FillRectangle(back, area);
            var state = g.Save(); float scale = Math.Min(area.Width / 800f, area.Height / 360f);
            g.TranslateTransform(area.Left + (area.Width - 800 * scale) / 2, area.Top + (area.Height - 360 * scale) / 2); g.ScaleTransform(scale, scale);
            using (var font = new Font("Segoe UI", 11))
            using (var small = new Font("Segoe UI", 9))
            {
                var totals = WorkJournal.Totals(raw);
                DrawText(g, I18n.T(mode == 2 ? "hourly_work" : "task_distribution"), font, ink, new RectangleF(20,8,760,30), I18n.IsRtl);
                if (totals.Length == 0 || totals.Sum(e=>e.Seconds) <= 0) { DrawText(g,I18n.T("no_work"),font,muted,new RectangleF(30,130,730,60),I18n.IsRtl); g.Restore(state); return; }
                if (mode == 2)
                {
                    var hours = WorkJournal.Hourly(raw); double max = Math.Max(1, hours.Max()/60);
                    float left=64, top=60, width=700, height=238;
                    using(var pen = new Pen(print ? Color.FromArgb(220,228,234) : Theme.Border))
                    for(int i=0;i<=4;i++) { float y=top+height-height*i/4; g.DrawLine(pen,left,y,left+width,y); DrawText(g,(max*i/4).ToString("0.#",CultureInfo.InvariantCulture),small,muted,new RectangleF(2,y-10,56,20)); }
                    var points = hours.Select((value,i)=>new PointF(left+width*i/23,top+height-(float)(height*value/60/max))).ToArray();
                    using(var pen = new Pen(Colors[0],3)) g.DrawLines(pen,points);
                    foreach(var point in points) using(var fill=new SolidBrush(Colors[0])) g.FillEllipse(fill,point.X-3,point.Y-3,6,6);
                    foreach(int hour in new[]{0,4,8,12,16,20,23}) DrawText(g,hour.ToString("00")+":00",small,muted,new RectangleF(left+width*hour/23-30,302,78,24),false,true);
                    DrawText(g,I18n.T("chart_minutes"),small,muted,new RectangleF(4,35,150,20));
                    if(raw.Any(e=>string.IsNullOrEmpty(e.StartedAt))) DrawText(g,I18n.T("legacy_hours"),small,muted,new RectangleF(20,332,760,24),I18n.IsRtl);
                }
                else
                {
                    var shown=totals.Take(7).ToList();
                    if(totals.Length>7) shown.Add(new WorkEntry{Task=I18n.T("other_tasks"), Seconds=totals.Skip(7).Sum(e=>e.Seconds)});
                    double sum=totals.Sum(e=>e.Seconds);
                    if(mode==0)
                    {
                        float angle=-90;
                        for(int i=0;i<shown.Count;i++) { float sweep=(float)(360*shown[i].Seconds/sum); using(var fill=new SolidBrush(Colors[i])) g.FillPie(fill,35,65,250,250,angle,sweep); angle+=sweep; }
                        using(var hole=new SolidBrush(print ? Color.White : Theme.Surface)) g.FillEllipse(hole,98,128,124,124);
                        for(int i=0;i<shown.Count;i++)
                        {
                            float y=54+i*35; using(var fill=new SolidBrush(Colors[i])) g.FillEllipse(fill,325,y+9,12,12);
                            DrawText(g,shown[i].Task,font,ink,new RectangleF(350,y,260,32),I18n.IsRtl);
                            DrawText(g,(100*shown[i].Seconds/sum).ToString("0.#")+"%  ·  "+WorkJournal.Format(shown[i].Seconds),small,muted,new RectangleF(620,y,160,32));
                        }
                    }
                    else
                    {
                        double max=shown.Max(e=>e.Seconds);
                        for(int i=0;i<shown.Count;i++)
                        {
                            float y=48+i*35;
                            DrawText(g,shown[i].Task,font,ink,new RectangleF(16,y,210,30),I18n.IsRtl);
                            using(var fill=new SolidBrush(Colors[i])) g.FillRectangle(fill,240,y+7,(float)(390*shown[i].Seconds/max),18);
                            DrawText(g,WorkJournal.Format(shown[i].Seconds),small,muted,new RectangleF(645,y,145,30));
                        }
                    }
                }
            }
            g.Restore(state);
        }
        public static Bitmap Report(WorkEntry[] entries, DateTime day, int mode, int first, int count, int page = 1, int pages = 1)
        {
            var totals=WorkJournal.Totals(entries);
            int height=650+count*88;
            var image=new Bitmap(1400,height); image.SetResolution(150,150);
            using(var g=Graphics.FromImage(image))
            using(var title=new Font("Segoe UI",23,FontStyle.Bold))
            using(var font=new Font("Segoe UI",22,GraphicsUnit.Pixel))
            {
                g.Clear(Color.White);
                DrawText(g,"RestTimer · "+I18n.T("dashboard"),title,Color.FromArgb(26,40,56),new RectangleF(48,18,1304,64),I18n.IsRtl);
                DrawText(g,day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"   ·   "+I18n.T("total_time")+" "+WorkJournal.Format(totals.Sum(e=>e.Seconds)),font,Color.FromArgb(83,103,120),new RectangleF(48,85,1304,44),I18n.IsRtl);
                Draw(g,new Rectangle(48,145,1304,400),entries,mode,true);
                DrawText(g,I18n.T("task_name"),font,Color.Black,new RectangleF(I18n.IsRtl?380:60,555,950,44),I18n.IsRtl);
                DrawText(g,I18n.T("time_spent"),font,Color.Black,new RectangleF(I18n.IsRtl?60:1050,555,300,44));
                for(int i=0;i<count;i++)
                {
                    int y=604+i*88; var row=totals[first+i];
                    if(i%2==0) using(var fill=new SolidBrush(Color.FromArgb(244,247,250))) g.FillRectangle(fill,48,y,1304,88);
                    DrawText(g,row.Task,font,Color.FromArgb(26,40,56),new RectangleF(I18n.IsRtl?380:60,y,950,88),I18n.IsRtl);
                    DrawText(g,WorkJournal.Format(row.Seconds),font,Color.FromArgb(26,40,56),new RectangleF(I18n.IsRtl?60:1050,y,300,88));
                }
                DrawText(g,page+" / "+pages,font,Color.Gray,new RectangleF(48,height-36,1304,30));
            }
            return image;
        }
    }

    // Raster pages preserve all localized glyphs without requiring fonts on the PDF reader.
    internal static class DashboardPdf
    {
        public static void Write(string path, IEnumerable<Bitmap> pages)
        {
            using(var file=new FileStream(path,FileMode.Create,FileAccess.Write))
            {
                Action<string> write=s=>{var bytes=Encoding.ASCII.GetBytes(s);file.Write(bytes,0,bytes.Length);};
                var offsets=new List<long>{0,0,0}; var kids=new List<int>();
                write("%PDF-1.4\n");
                int index=0;
                foreach(var bitmap in pages)
                using(bitmap)
                using(var jpeg=new MemoryStream())
                {
                    var encoder=ImageCodecInfo.GetImageEncoders().Single(e=>e.MimeType=="image/jpeg");
                    using(var quality=new EncoderParameters(1)) { quality.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,95L); bitmap.Save(jpeg,encoder,quality); }
                    byte[] image=jpeg.ToArray(); int page=3+index*3, resource=page+1, content=page+2; kids.Add(page);
                    while(offsets.Count<=content) offsets.Add(0);
                    offsets[page]=file.Position;
                    write(page+" 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /XObject << /Im "+resource+" 0 R >> >> /Contents "+content+" 0 R >>\nendobj\n");
                    offsets[resource]=file.Position;
                    write(resource+" 0 obj\n<< /Type /XObject /Subtype /Image /Width "+bitmap.Width+" /Height "+bitmap.Height+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "+image.Length+" >>\nstream\n");file.Write(image,0,image.Length);write("\nendstream\nendobj\n");
                    double scale=Math.Min(547.28/bitmap.Width,793.89/bitmap.Height), width=bitmap.Width*scale,height=bitmap.Height*scale;
                    string drawing=string.Format(CultureInfo.InvariantCulture,"q {0:F3} 0 0 {1:F3} 24 {2:F3} cm /Im Do Q",width,height,817.89-height);
                    offsets[content]=file.Position;write(content+" 0 obj\n<< /Length "+Encoding.ASCII.GetByteCount(drawing)+" >>\nstream\n"+drawing+"\nendstream\nendobj\n"); index++;
                }
                offsets[1]=file.Position;write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                offsets[2]=file.Position;write("2 0 obj\n<< /Type /Pages /Count "+kids.Count+" /Kids ["+string.Join(" ",kids.Select(k=>k+" 0 R"))+"] >>\nendobj\n");
                long xref=file.Position;write("xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");
                foreach(long offset in offsets.Skip(1)) write(offset.ToString("0000000000",CultureInfo.InvariantCulture)+" 00000 n \n");
                write("trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref.ToString(CultureInfo.InvariantCulture)+"\n%%EOF\n");file.Flush(true);
            }
        }
    }

    internal sealed class WorkReportPanel : Panel
    {
        private readonly DateTimePicker day;
        private readonly ListView rows;
        private readonly Label total;
        private readonly DashboardChart chart;
        private readonly ComboBox chartType;
        private readonly Button png, pdf;
        private WorkEntry[] entries=new WorkEntry[0];
        public WorkReportPanel()
        {
            Dock=DockStyle.Fill; Padding=new Padding(20);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6};
            foreach(int height in new[]{48,42,42,40}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,62));layout.RowStyles.Add(new RowStyle(SizeType.Percent,38));
            layout.Controls.Add(new Label{Name="i18n:dashboard",Text=I18n.T("dashboard"),Font=Theme.HeadingFont,Dock=DockStyle.Fill},0,0);
            day=new DateTimePicker{Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill,AccessibleName=I18n.T("report_date")};layout.Controls.Add(day,0,1);
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            chartType=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=I18n.T("chart_type")};
            chartType.Items.AddRange(new object[]{I18n.T("pie_chart"),I18n.T("bar_chart"),I18n.T("line_chart")});chartType.SelectedIndex=0;
            png=new RoundedButton{Text="PNG",Dock=DockStyle.Fill,AccessibleName=I18n.T("export_png")};pdf=new RoundedButton{Text="PDF",Dock=DockStyle.Fill,AccessibleName=I18n.T("export_pdf")};
            actions.Controls.Add(chartType,0,0);actions.Controls.Add(png,1,0);actions.Controls.Add(pdf,2,0);layout.Controls.Add(actions,0,2);
            total=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};layout.Controls.Add(total,0,3);
            chart=new DashboardChart();layout.Controls.Add(chart,0,4);
            rows=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,BorderStyle=BorderStyle.None,HeaderStyle=ColumnHeaderStyle.Nonclickable};
            rows.Columns.Add(I18n.T("task_name"),300);rows.Columns.Add(I18n.T("time_spent"),150);layout.Controls.Add(rows,0,5);Controls.Add(layout);
            chartType.SelectedIndexChanged+=(_,__)=>{chart.Mode=chartType.SelectedIndex;chart.Invalidate();};
            day.ValueChanged+=(_,__)=>RefreshReport();VisibleChanged+=(_,__)=>{if(Visible) RefreshReport();};
            png.Click+=(_,__)=>ChooseExport(false);pdf.Click+=(_,__)=>ChooseExport(true);
        }
        public void RefreshReport()
        {
            int selected=chartType.SelectedIndex;
            string[] keys={"pie_chart","bar_chart","line_chart"}; for(int i=0;i<3;i++) chartType.Items[i]=I18n.T(keys[i]); chartType.SelectedIndex=selected;
            rows.Columns[0].Text=I18n.T("task_name");rows.Columns[1].Text=I18n.T("time_spent");rows.BackColor=Theme.Surface;rows.ForeColor=Theme.Text;rows.Items.Clear();
            try {
                entries=new WorkJournal().ReadRaw(day.Value);var totals=WorkJournal.Totals(entries);
                foreach(var entry in totals) rows.Items.Add(new ListViewItem(new[]{entry.Task,WorkJournal.Format(entry.Seconds)}));
                total.Text=totals.Length==0?I18n.T("no_work"):I18n.T("total_time")+"  "+WorkJournal.Format(totals.Sum(e=>e.Seconds))+"    ·    "+I18n.T("task_count")+" "+totals.Length;
                png.Enabled=pdf.Enabled=true;
            } catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.Serialization.SerializationException) { entries=new WorkEntry[0];total.Text=I18n.T("log_error");png.Enabled=pdf.Enabled=false; }
            chart.Entries=entries;chart.Invalidate();
        }
        private IEnumerable<Bitmap> Pages()
        {
            int count=WorkJournal.Totals(entries).Length,pages=Math.Max(1,(count+13)/14);
            for(int i=0;i<pages;i++) yield return DashboardChart.Report(entries,day.Value,chartType.SelectedIndex,i*14,Math.Min(14,count-i*14),i+1,pages);
        }
        public void Export(string path, bool asPdf)
        {
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                if(asPdf) DashboardPdf.Write(temporary,Pages());
                else using(var image=DashboardChart.Report(entries,day.Value,chartType.SelectedIndex,0,WorkJournal.Totals(entries).Length)) image.Save(temporary,ImageFormat.Png);
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
        private void ChooseExport(bool asPdf)
        {
            using(var dialog=new SaveFileDialog{Filter=asPdf?"PDF|*.pdf":"PNG|*.png",FileName="RestTimer-"+day.Value.ToString("yyyy-MM-dd")+(asPdf?".pdf":".png"),Title=I18n.T(asPdf?"export_pdf":"export_png")})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                try { Export(dialog.FileName,asPdf);total.Text=I18n.T("export_saved"); }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.InteropServices.ExternalException || ex is ArgumentException) { MessageBox.Show(this,I18n.T("export_error"),"RestTimer",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            }
        }
    }
}
