using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace RestTimer
{
    [DataContract]
    internal sealed class WorkEntry
    {
        [DataMember] public string Day;
        [DataMember] public string Task;
        [DataMember] public double Seconds;
        [DataMember(EmitDefaultValue = false)] public string StartedAt;
    }

    internal sealed class WorkJournal
    {
        private readonly string folder;
        public WorkJournal(string folder = null) { this.folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestTimer", "work-log"); }
        public static WorkEntry[] Split(string task, DateTimeOffset start, double seconds)
        {
            var entries = new List<WorkEntry>();
            while (seconds > 0.000001)
            {
                var local = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.Local);
                var midnight = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified));
                double part = Math.Min(seconds, (midnight - start.UtcDateTime).TotalSeconds);
                entries.Add(new WorkEntry { Day = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Task = task, Seconds = part, StartedAt = start.ToString("o", CultureInfo.InvariantCulture) });
                start = start.AddSeconds(part); seconds -= part;
            }
            return entries.ToArray();
        }
        public void Save(string id, string task, DateTimeOffset start, double seconds)
        {
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, id + ".json"), temp = target + ".tmp";
            if (File.Exists(target)) return;
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            { new DataContractJsonSerializer(typeof(WorkEntry[])).WriteObject(stream, Split(task, start, seconds)); stream.Flush(true); }
            File.Move(temp, target);
        }
        public WorkEntry[] ReadRaw(DateTime day)
        {
            if (!Directory.Exists(folder)) return new WorkEntry[0];
            string key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var all = new List<WorkEntry>();
            foreach (string file in Directory.GetFiles(folder, "*.json"))
                using (var stream = File.OpenRead(file))
                    all.AddRange(((WorkEntry[])new DataContractJsonSerializer(typeof(WorkEntry[])).ReadObject(stream)).Where(e => e.Day == key));
            return all.ToArray();
        }
        public WorkEntry[] Read(DateTime day) => Totals(ReadRaw(day));
        public static WorkEntry[] Totals(WorkEntry[] entries) => entries.GroupBy(e => e.Task).Select(g => new WorkEntry { Task = g.Key, Seconds = g.Sum(e => e.Seconds) }).OrderByDescending(e => e.Seconds).ThenBy(e => e.Task).ToArray();
        public static double[] Hourly(WorkEntry[] entries)
        {
            var hours = new double[24];
            foreach (var entry in entries)
            {
                if (!DateTimeOffset.TryParse(entry.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)) continue;
                double remaining = entry.Seconds;
                while (remaining > 0.000001)
                {
                    var local = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.Local);
                    double part = Math.Min(remaining, 3600 - local.Minute * 60 - local.Second - local.Millisecond / 1000.0);
                    hours[local.Hour] += part; start = start.AddSeconds(part); remaining -= part;
                }
            }
            return hours;
        }
        public static string Format(double seconds) { var time = TimeSpan.FromSeconds(Math.Floor(seconds)); return ((int)time.TotalHours).ToString("00") + time.ToString(@"\:mm\:ss"); }
    }

    internal sealed class WorkStartDialog : Form
    {
        public string TaskName { get; private set; }
        public int Seconds { get; private set; }
        public WorkStartDialog(int seconds, string task)
        {
            Text = I18n.T("new_session"); Font = Theme.BodyFont; RightToLeft = I18n.Flow;
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96,96);
            ClientSize = new Size(536, 328);
            var title = new Label { Text = I18n.T("task_name"), Bounds = new Rectangle(28, 20, 480, 30) };
            var name = new TextBox { Text = task ?? "", MaxLength = 120, Bounds = new Rectangle(28, 56, 480, 32), AccessibleName = title.Text };
            var duration = new DurationEditor(I18n.T("work_duration"), I18n.T("session_hint"), seconds) { Location = new Point(28, 104) };
            var error = new Label { Bounds = new Rectangle(28, 238, 480, 32), ForeColor = Theme.Error, Tag="error" };
            var begin = new RoundedButton(true) { Text = I18n.T("new_session"), Bounds = new Rectangle(268, 280, 240, 34) };
            error.TextChanged+=(_,__)=>
            {
                float z=Theme.ScaleFor(this);
                error.Height=Math.Max((int)(32*z),TextRenderer.MeasureText(error.Text,error.Font,new Size(Math.Max(1,error.Width),32767),TextFormatFlags.WordBreak).Height+(int)(4*z));
                begin.Top=error.Bottom+(int)(10*z);ClientSize=new Size(ClientSize.Width,Math.Max((int)(328*z),begin.Bottom+(int)(14*z)));
            };
            begin.Click += (_, __) => {
                if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = I18n.T("task_required"); name.Focus(); return; }
                if (!duration.TryGetSeconds(out int value) || value < 1 || value > Preferences.MaxWorkSeconds) { error.Text = I18n.T("work_validation"); return; }
                TaskName = name.Text.Trim(); Seconds = value; DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { title, name, duration, error, begin }); AcceptButton = begin; Theme.Apply(this);
        }
    }

}
