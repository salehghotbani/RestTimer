using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace RestTimer
{
    // Calendar choice affects presentation only; journals always use ISO dates.
    internal static class DateDisplay
    {
        public static string Preference = "auto";
        public static string Normalize(string value) => value == "persian" || value == "gregorian" || value == "hijri" ? value : "auto";
        public static string Kind => Preference == "auto" ? I18n.Code == "fa" ? "persian" : I18n.Code == "ar" ? "hijri" : "gregorian" : Preference;
        public static Calendar Calendar => Kind == "persian" ? (Calendar)new PersianCalendar() : Kind == "hijri" ? (Calendar)new HijriCalendar { HijriAdjustment=-1 } : new GregorianCalendar();
        public static string[] Months => Kind == "persian"
            ? (I18n.IsRtl ? "فروردین,اردیبهشت,خرداد,تیر,مرداد,شهریور,مهر,آبان,آذر,دی,بهمن,اسفند" : "Farvardin,Ordibehesht,Khordad,Tir,Mordad,Shahrivar,Mehr,Aban,Azar,Dey,Bahman,Esfand").Split(',')
            : Kind == "hijri" ? (I18n.IsRtl ? "محرم,صفر,ربیع‌الاول,ربیع‌الثانی,جمادی‌الاول,جمادی‌الثانی,رجب,شعبان,رمضان,شوال,ذیقعده,ذیحجه" : "Muharram,Safar,Rabi I,Rabi II,Jumada I,Jumada II,Rajab,Shaban,Ramadan,Shawwal,Dhu al-Qidah,Dhu al-Hijjah").Split(',')
            : I18n.Code=="fa" ? "ژانویه,فوریه,مارس,آوریل,مه,ژوئن,ژوئیه,اوت,سپتامبر,اکتبر,نوامبر,دسامبر".Split(',')
            : I18n.Code=="ar" ? "يناير,فبراير,مارس,أبريل,مايو,يونيو,يوليو,أغسطس,سبتمبر,أكتوبر,نوفمبر,ديسمبر".Split(',') : Culture.DateTimeFormat.MonthNames;
        public static CultureInfo Culture { get { try { var culture=(CultureInfo)CultureInfo.GetCultureInfo(I18n.Code).Clone();var gregorian=culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();if(gregorian!=null) culture.DateTimeFormat.Calendar=gregorian;return culture; } catch(CultureNotFoundException) { return CultureInfo.InvariantCulture; } } }
        public static DateTime Minimum => Kind=="hijri" ? new DateTime(622,7,19) : Calendar.MinSupportedDateTime.Date;
        public static string Digits(int value) { string text=value.ToString(CultureInfo.InvariantCulture); if(I18n.Code=="fa") for(int i=0;i<10;i++) text=text.Replace((char)('0'+i),(char)('۰'+i)); return text; }
        public static DateTime Clamp(DateTime value) => value < Minimum ? Minimum : value > Calendar.MaxSupportedDateTime ? Calendar.MaxSupportedDateTime.Date : value.Date;
        public static string Format(DateTime date)
        {
            date=Clamp(date); var cal=Calendar;
            return Digits(cal.GetDayOfMonth(date))+" "+Months[cal.GetMonth(date)-1]+" "+Digits(cal.GetYear(date));
        }
    }

    internal sealed class CalendarDatePicker : UserControl
    {
        private DateTime value = DateTime.Today;
        private readonly RoundedButton button;
        private ToolStripDropDown popup;
        public DateTime Value { get => value; set { var next=DateDisplay.Clamp(value); bool changed=this.value!=next; this.value=next; RefreshCaption(); if(changed) ValueChanged?.Invoke(this,EventArgs.Empty); } }
        public event EventHandler ValueChanged;
        public CalendarDatePicker()
        {
            Height=40;
            button=new RoundedButton { Dock=DockStyle.Fill, Glyph="calendar", StartAligned=true };
            Controls.Add(button); button.Click+=(_,__)=>OpenCalendar(); RefreshCaption();
        }
        public void RefreshCaption() { value=DateDisplay.Clamp(value); button.RightToLeft=I18n.Flow; button.Text=DateDisplay.Format(value); button.AccessibleName=I18n.T("report_date")+": "+button.Text; button.Invalidate(); }
        public void OpenCalendar()
        {
            popup?.Dispose();
            var calendar=new CalendarGrid(value, date=>{ Value=date; popup.Close(); });
            float z=Theme.ScaleFor(this);calendar.Scale(new SizeF(z,z));
            var host=new ToolStripControlHost(calendar) { AutoSize=false, Size=calendar.Size, Margin=Padding.Empty, Padding=Padding.Empty };
            popup=new ToolStripDropDown { Padding=new Padding(1), BackColor=Theme.Border, AutoClose=true };
            popup.Items.Add(host);
            var area=Screen.FromControl(this).WorkingArea;var origin=PointToScreen(new Point(I18n.IsRtl?Width-calendar.Width:0,Height+4));
            origin.X=Math.Max(area.Left,Math.Min(origin.X,area.Right-popup.PreferredSize.Width));origin.Y=Math.Max(area.Top,Math.Min(origin.Y,area.Bottom-popup.PreferredSize.Height));
            popup.Show(origin);
        }
        protected override void Dispose(bool disposing) { if(disposing) popup?.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class CalendarGrid : UserControl
    {
        private DateTime month, selected;
        private readonly Action<DateTime> choose;
        private readonly TableLayoutPanel days;
        private readonly ComboBox months;
        private readonly NumericUpDown year;
        private readonly RoundedButton previous, next;
        private bool updating;
        public CalendarGrid(DateTime value, Action<DateTime> choose)
        {
            this.choose=choose; selected=value; month=value;
            Font=Theme.BodyFont; Size=new Size(322,354); Padding=new Padding(12); BackColor=Theme.Surface;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=4, RightToLeft=RightToLeft.No };
            foreach(int h in new[]{38,26}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            var header=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=4, RightToLeft=I18n.Flow };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,30));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,30));
            previous=new RoundedButton { Text=I18n.IsRtl?"›":"‹", Dock=DockStyle.Fill, AccessibleName=I18n.T("previous_month") };
            next=new RoundedButton { Text=I18n.IsRtl?"‹":"›", Dock=DockStyle.Fill, AccessibleName=I18n.T("next_month") };
            months=new StyledComboBox { Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList, FlatStyle=FlatStyle.Flat, AccessibleName=I18n.T("month") };
            months.RightToLeft=I18n.Flow;months.DrawMode=DrawMode.OwnerDrawFixed;months.ItemHeight=Font.Height+4;
            months.DrawItem+=(_,e)=> { if(e.Index<0) return;bool active=(e.State&DrawItemState.Selected)!=0;
                using(var brush=new SolidBrush(active?Theme.Selection:Theme.Input)) e.Graphics.FillRectangle(brush,e.Bounds);
                TextRenderer.DrawText(e.Graphics,months.Items[e.Index].ToString(),Font,Rectangle.Inflate(e.Bounds,-4,0),Theme.Text,
                    TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|(I18n.IsRtl?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left)); };
            months.Items.AddRange(DateDisplay.Months.Length==13 ? new ArraySegment<string>(DateDisplay.Months,0,12).ToArray() : DateDisplay.Months);
            var cal=DateDisplay.Calendar;
            year=new NumericUpDown { Dock=DockStyle.Fill, RightToLeft=RightToLeft.No, Minimum=cal.GetYear(DateDisplay.Minimum),Maximum=cal.GetYear(cal.MaxSupportedDateTime), BorderStyle=BorderStyle.None, AccessibleName=I18n.T("year") };
            header.Controls.Add(previous,0,0);header.Controls.Add(months,1,0);header.Controls.Add(year,2,0);header.Controls.Add(next,3,0);layout.Controls.Add(header,0,0);
            var weekdays=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=7, RightToLeft=I18n.Flow };
            int first=I18n.IsRtl ? 6 : (int)DateDisplay.Culture.DateTimeFormat.FirstDayOfWeek;
            string[] weekNames=I18n.Code=="fa" ? new[]{"ی","د","س","چ","پ","ج","ش"} : I18n.Code=="ar" ? new[]{"ح","ن","ث","ر","خ","ج","س"} : DateDisplay.Culture.DateTimeFormat.ShortestDayNames;
            for(int i=0;i<7;i++) { weekdays.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7)); weekdays.Controls.Add(new Label { Dock=DockStyle.Fill, Text=weekNames[(first+i)%7], TextAlign=ContentAlignment.MiddleCenter, ForeColor=Theme.Muted },i,0); }
            layout.Controls.Add(weekdays,0,1);
            days=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=7, RowCount=6, RightToLeft=I18n.Flow };
            for(int i=0;i<7;i++) days.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));for(int i=0;i<6;i++) days.RowStyles.Add(new RowStyle(SizeType.Percent,100f/6));layout.Controls.Add(days,0,2);
            var today=new RoundedButton { Text=I18n.T("today"), Dock=DockStyle.Fill };today.Click+=(_,__)=>choose(DateTime.Today);layout.Controls.Add(today,0,3);Controls.Add(layout);
            previous.Click+=(_,__)=>MoveMonth(-1);next.Click+=(_,__)=>MoveMonth(1);
            months.SelectedIndexChanged+=(_,__)=>ChangeMonth();year.ValueChanged+=(_,__)=>ChangeMonth();
            UpdateDays();
        }
        private void MoveMonth(int count) { try { month=DateDisplay.Calendar.AddMonths(month,count);UpdateDays(); } catch(ArgumentOutOfRangeException) { } }
        private void ChangeMonth()
        {
            if(updating || months.SelectedIndex<0) return;
            try { month=DateDisplay.Calendar.ToDateTime((int)year.Value,months.SelectedIndex+1,1,0,0,0,0);UpdateDays(); }
            catch(ArgumentOutOfRangeException) { UpdateDays(); }
        }
        private void UpdateDays()
        {
            updating=true;var cal=DateDisplay.Calendar;int m=cal.GetMonth(month),y=cal.GetYear(month);months.SelectedIndex=m-1;year.Value=y;
            days.SuspendLayout();while(days.Controls.Count>0) days.Controls[0].Dispose();
            DateTime start=month.AddDays(1-cal.GetDayOfMonth(month));int first=I18n.IsRtl ? 6 : (int)DateDisplay.Culture.DateTimeFormat.FirstDayOfWeek;int offset=((int)start.DayOfWeek-first+7)%7;
            for(int i=0;i<42;i++)
            {
                DateTime date;try { date=start.AddDays(i-offset); } catch(ArgumentOutOfRangeException) { continue; }
                if(date<DateDisplay.Minimum || date>cal.MaxSupportedDateTime) continue;
                var day=new RoundedButton { Text=DateDisplay.Digits(cal.GetDayOfMonth(date)), Quiet=true, Dock=DockStyle.Fill, Margin=new Padding(2), Selected=date.Date==selected.Date, Enabled=cal.GetMonth(date)==m, AccessibleName=DateDisplay.Format(date) };
                day.Click+=(_,__)=>choose(date);days.Controls.Add(day,i%7,i/7);
            }
            previous.Enabled=start>DateDisplay.Minimum.AddDays(31);next.Enabled=start<cal.MaxSupportedDateTime.AddDays(-31);
            days.ResumeLayout();updating=false;Theme.Apply(this);BackColor=Theme.Surface;
        }
    }
}
