using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class UpdateDialog : Form
    {
        private readonly UpdateService service;
        private readonly Label description;
        private readonly Label progressText;
        private readonly RoundedButton install;
        private readonly RoundedButton check;
        private readonly RichTextBox latestNotes;
        private readonly RichTextBox historyNotes;
        private readonly ComboBox versions;
        private readonly ProgressBar progress;
        private readonly TabControl tabs;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool working;
        private bool installing;
        private readonly bool checkOnShow;

        public UpdateDialog(Icon icon, UpdateService service = null, bool history = false, bool checkOnShow = true)
        {
            this.service = service ?? new UpdateService();
            this.checkOnShow = checkOnShow;
            Text = "به‌روزرسانی و تغییرات — RestTimer";
            Icon = icon;
            Font = Theme.BodyFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            RightToLeft = RightToLeft.Yes;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(700, 644);
            var heading = new Label { Text = "به‌روزرسانی و تغییرات", Font = Theme.HeadingFont,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(24, 18, 652, 38) };
            description = new Label { ForeColor = Theme.Mint, TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(24, 60, 652, 32) };
            var daily = new CheckBox { Text = "بررسی خودکار روزانه و اعلان نسخهٔ جدید", Checked = this.service.State.DailyEnabled,
                Bounds = new Rectangle(24, 96, 652, 28) };
            daily.CheckedChanged += (_, __) => { this.service.State.DailyEnabled = daily.Checked; this.service.Save(); };
            tabs = new TabControl { Bounds = new Rectangle(24, 138, 652, 334), RightToLeftLayout = true };
            var latest = new TabPage("آخرین نسخه") { BackColor = Theme.Surface };
            latestNotes = NotesBox();
            latest.Controls.Add(latestNotes);
            var historyTab = new TabPage("تاریخچهٔ نسخه‌ها") { BackColor = Theme.Surface };
            historyNotes = NotesBox();
            versions = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.Input, ForeColor = Theme.Text, Font = Theme.BodyFont };
            versions.Items.Add("تغییرات همراه برنامه (آفلاین)");
            versions.SelectedIndexChanged += (_, __) => historyNotes.Text = versions.SelectedItem is ReleaseInfo release
                ? release + "\n\n" + release.Notes : UpdateService.BundledHistory();
            historyTab.Controls.Add(historyNotes);
            historyTab.Controls.Add(versions);
            tabs.TabPages.AddRange(new[] { latest, historyTab });
            tabs.SelectedIndex = history ? 1 : 0;
            versions.SelectedIndex = 0;
            var consent = new Label { Text = "با انتخاب «دانلود و نصب»، به‌روزرسانی انجام می‌شود. برنامه هنگام نصب بسته و پس از پایان دوباره اجرا می‌شود.",
                ForeColor = Theme.Muted, Font = Theme.SmallFont, TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(24, 482, 652, 42) };
            progress = new ProgressBar { Minimum = 0, Maximum = 100, Bounds = new Rectangle(24, 538, 652, 8) };
            progressText = new Label { ForeColor = Theme.Muted, Font = Theme.SmallFont,
                TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(24, 548, 652, 30) };
            install = new RoundedButton(primary: true) { Text = "دانلود و نصب", Enabled = false, Bounds = new Rectangle(420, 590, 256, 38) };
            install.Click += async (_, __) => await InstallAsync();
            check = new RoundedButton { Text = "بررسی دوباره", Bounds = new Rectangle(224, 590, 180, 38) };
            check.Click += async (_, __) => await RefreshAsync();
            var close = new RoundedButton { Text = "بستن / لغو", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(24, 590, 180, 38) };
            Controls.AddRange(new Control[] { heading, description, daily, tabs, consent, progress, progressText, install, check, close });
            CancelButton = close;
            ShowLatest();
        }
        private static RichTextBox NotesBox() => new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true,
            BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
            Font = Theme.BodyFont, DetectUrls = false, RightToLeft = RightToLeft.Yes };
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindowFrame(Handle); }
        protected override async void OnShown(EventArgs e) { base.OnShown(e); if (checkOnShow) await RefreshAsync(); }
        private void ShowLatest()
        {
            var release = service.State.Latest;
            bool newer = UpdateClient.IsStable(release) && release.Version > UpdateClient.CurrentVersion;
            description.Text = "نسخهٔ نصب‌شده  \u200e" + UpdateClient.CurrentVersion.ToString(3)
                + (newer ? "    ·    نسخهٔ جدید  \u200e" + release.Version.ToString(3) : "");
            latestNotes.Text = release == null ? UpdateService.BundledHistory() : release + "\n\n" + (release.Notes ?? "توضیحی برای این نسخه ثبت نشده است.");
            install.Enabled = newer && !working && !installing;
            install.Text = newer ? "دانلود و نصب " + release.Version.ToString(3) : "دانلود و نصب";
        }
        private async Task RefreshAsync()
        {
            if (working || installing) return;
            working = true;
            check.Enabled = install.Enabled = false;
            progressText.Text = "در حال بررسی نسخه و دریافت تغییرات…";
            try
            {
                await service.CheckAsync(cancellation.Token);
                if (IsDisposed) return;
                ShowLatest();
                progressText.Text = service.State.Latest != null && service.State.Latest.Version > UpdateClient.CurrentVersion
                    ? "نسخهٔ جدید آماده است. تغییرات را بخوانید و برای نصب تأیید کنید." : "برنامهٔ شما به‌روز است.";
                var releases = await UpdateClient.GetHistoryAsync(cancellation.Token);
                if (IsDisposed) return;
                versions.Items.Clear();
                versions.Items.Add("تغییرات همراه برنامه (آفلاین)");
                foreach (var release in releases) versions.Items.Add(release);
                versions.SelectedIndex = versions.Items.Count > 1 ? 1 : 0;
            }
            catch (Exception ex) when (ExpectedFailure(ex))
            {
                if (!IsDisposed) progressText.Text = "اتصال برقرار نشد. تغییرات ذخیره‌شده در دسترس است؛ دوباره تلاش کنید.";
            }
            finally
            {
                working = false;
                if (!IsDisposed) { check.Enabled = true; ShowLatest(); }
            }
        }
        private async Task InstallAsync()
        {
            if (working || installing || !install.Enabled) return;
            working = true;
            install.Enabled = check.Enabled = false;
            progress.Value = 0;
            progressText.Text = "در حال دانلود… برای لغو، پنجره را ببندید.";
            try
            {
                var file = await UpdateClient.DownloadAsync(service.State.Latest, new Progress<int>(value =>
                {
                    if (!IsDisposed && !installing) { progress.Value = value; progressText.Text = "در حال دانلود و بررسی فایل…  " + value + "٪"; }
                }), cancellation.Token);
                if (IsDisposed) return;
                UpdateClient.Install(file);
                installing = true;
                progressText.Text = "نصب شروع شد. برنامه به‌طور خودکار بسته و دوباره اجرا می‌شود.";
            }
            catch (Exception ex) when (ExpectedFailure(ex) || ex is Win32Exception || ex is InvalidOperationException)
            {
                if (!IsDisposed) progressText.Text = "دانلود یا اجرای نصاب ممکن نشد. دوباره تلاش کنید؛ فایل ناقص نصب نمی‌شود.";
            }
            finally
            {
                working = false;
                if (!IsDisposed) { check.Enabled = !installing; ShowLatest(); }
            }
        }
        internal static bool ExpectedFailure(Exception ex) => ex is HttpRequestException || ex is OperationCanceledException
            || ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException;
        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed) { cancellation.Cancel(); cancellation.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
