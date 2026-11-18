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
                entries.Add(new WorkEntry { Day = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Task = task, Seconds = part });
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
        public WorkEntry[] Read(DateTime day)
        {
            if (!Directory.Exists(folder)) return new WorkEntry[0];
            string key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var all = new List<WorkEntry>();
            foreach (string file in Directory.GetFiles(folder, "*.json"))
                using (var stream = File.OpenRead(file))
                    all.AddRange(((WorkEntry[])new DataContractJsonSerializer(typeof(WorkEntry[])).ReadObject(stream)).Where(e => e.Day == key));
            return all.GroupBy(e => e.Task).Select(g => new WorkEntry { Day = key, Task = g.Key, Seconds = g.Sum(e => e.Seconds) }).OrderByDescending(e => e.Seconds).ToArray();
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
            ClientSize = new Size(536, 328);
            var title = new Label { Text = I18n.T("task_name"), Bounds = new Rectangle(28, 20, 480, 30) };
            var name = new TextBox { Text = task ?? "", MaxLength = 120, Bounds = new Rectangle(28, 56, 480, 32), AccessibleName = title.Text };
            var duration = new DurationEditor(I18n.T("work_duration"), I18n.T("session_hint"), seconds) { Location = new Point(28, 104) };
            var error = new Label { Bounds = new Rectangle(28, 238, 480, 32), ForeColor = Theme.Error };
            var begin = new RoundedButton(true) { Text = I18n.T("new_session"), Bounds = new Rectangle(268, 280, 240, 34) };
            begin.Click += (_, __) => {
                if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = I18n.T("task_required"); name.Focus(); return; }
                if (!duration.TryGetSeconds(out int value) || value < 1 || value > Preferences.MaxWorkSeconds) { error.Text = I18n.T("work_validation"); return; }
                TaskName = name.Text.Trim(); Seconds = value; DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { title, name, duration, error, begin }); AcceptButton = begin; Theme.Apply(this);
        }
    }

    internal sealed class WorkReportPanel : Panel
    {
        private readonly DateTimePicker day;
        private readonly ListView rows;
        private readonly Label total;
        public WorkReportPanel()
        {
            Dock = DockStyle.Fill; Padding = new Padding(24);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.Controls.Add(new Label { Name = "i18n:daily_report", Text = I18n.T("daily_report"), Font = Theme.HeadingFont, Dock = DockStyle.Fill }, 0, 0);
            day = new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill, MaxDate = DateTime.Today.AddDays(1), AccessibleName = I18n.T("report_date") };
            rows = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            rows.Columns.Add(I18n.T("task_name"), 300); rows.Columns.Add(I18n.T("time_spent"), 150);
            total = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(day, 0, 1); layout.Controls.Add(rows, 0, 2); layout.Controls.Add(total, 0, 3); Controls.Add(layout);
            day.ValueChanged += (_, __) => RefreshReport(); VisibleChanged += (_, __) => { if (Visible) RefreshReport(); };
        }
        public void RefreshReport()
        {
            rows.Columns[0].Text = I18n.T("task_name"); rows.Columns[1].Text = I18n.T("time_spent");
            rows.BackColor = Theme.Surface; rows.ForeColor = Theme.Text; rows.Items.Clear();
            try {
                var entries = new WorkJournal().Read(day.Value);
                foreach (var entry in entries) rows.Items.Add(new ListViewItem(new[] { entry.Task, WorkJournal.Format(entry.Seconds) }));
                total.Text = entries.Length == 0 ? I18n.T("no_work") : I18n.T("total_time") + "  " + WorkJournal.Format(entries.Sum(e => e.Seconds));
            } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException) { total.Text = I18n.T("log_error"); }
        }
    }
}
