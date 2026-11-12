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
        private readonly ReleaseNotesBox latestNotes;
        private readonly ReleaseNotesBox historyNotes;
        private readonly ListBox versions;
        private readonly UpdateProgress progress;
        private readonly RoundedButton latestTab, historyTab;
        private readonly Panel latestPanel, historyPanel;
        private readonly Label historyTitle;
        private readonly System.Windows.Forms.Timer themeTimer;
        private string paletteKey;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool working;
        private bool installing;
        private ReleaseInfo displayedRelease;
        private readonly bool checkOnShow;

        public UpdateDialog(Icon icon, UpdateService service = null, bool history = false, bool checkOnShow = true)
        {
            this.service = service ?? new UpdateService();
            this.checkOnShow = checkOnShow;
            Theme.Refresh();
            Text = I18n.T("updates_title") + " — RestTimer";
            Icon = icon;
            Font = Theme.BodyFont;
            RightToLeft = I18n.Flow;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            ClientSize = new Size(760, 700);
            MinimumSize = new Size(650, 550);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            header.Controls.Add(new Label { Text = I18n.T("updates_title"), Font = Theme.HeadingFont,
                TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Top, Height = 40 });
            description = new Label { Tag = "accent", Font = Theme.TitleFont, TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Bottom, Height = 38 };
            header.Controls.Add(description);
            layout.Controls.Add(header, 0, 0);
            var daily = new ThemedCheckBox { Text = I18n.T("daily_check"), Checked = this.service.State.DailyEnabled,
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            daily.CheckedChanged += (_, __) => { this.service.State.DailyEnabled = daily.Checked; this.service.Save(); };
            layout.Controls.Add(daily, 0, 1);
            var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 8) };
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            latestTab = new RoundedButton { Text = I18n.T("latest"), Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0) };
            historyTab = new RoundedButton { Text = I18n.T("version_history"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 4, 0) };
            latestTab.Click += (_, __) => SelectPage(false);
            historyTab.Click += (_, __) => SelectPage(true);
            navigation.Controls.Add(latestTab, 0, 0);
            navigation.Controls.Add(historyTab, 1, 0);
            layout.Controls.Add(navigation, 0, 2);
            var content = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(18), Margin = Padding.Empty };
            latestPanel = new Panel { Dock = DockStyle.Fill };
            latestNotes = new ReleaseNotesBox();
            latestPanel.Controls.Add(latestNotes);
            latestPanel.Controls.Add(new Label { Text = I18n.T("release_notes"), Font = Theme.TitleFont, Dock = DockStyle.Top,
                Height = 36, TextAlign = ContentAlignment.MiddleLeft });
            historyPanel = new Panel { Dock = DockStyle.Fill };
            historyNotes = new ReleaseNotesBox();
            historyTitle = new Label { Text = I18n.T("version_history"), Font = Theme.TitleFont, Dock = DockStyle.Top,
                Height = 36, TextAlign = ContentAlignment.MiddleLeft };
            var historyBody = new Panel { Dock = DockStyle.Fill };
            historyBody.Controls.Add(historyNotes);
            historyBody.Controls.Add(historyTitle);
            versions = new ListBox { Dock = I18n.IsRtl ? DockStyle.Right : DockStyle.Left, Width = 154, BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 40, IntegralHeight = false, Font = Theme.BodyFont,
                AccessibleName = I18n.T("version_selector") };
            versions.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var brush = new SolidBrush(selected ? Theme.Selection : Theme.Surface)) e.Graphics.FillRectangle(brush, e.Bounds);
                string title = versions.Items[e.Index] is ReleaseInfo release ? I18n.T("version_label", release.Version.ToString(3)) : I18n.T("bundled_versions");
                TextRenderer.DrawText(e.Graphics, title, Theme.BodyFont, Rectangle.Inflate(e.Bounds, -10, 0),
                    selected ? Theme.OnSelection : Theme.Text, (I18n.IsRtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            };
            versions.Items.Add(I18n.T("bundled_history"));
            versions.SelectedIndexChanged += (_, __) =>
            {
                var release = versions.SelectedItem as ReleaseInfo;
                historyTitle.Text = release == null ? I18n.T("bundled_history") : I18n.T("history_version", release.Version.ToString(3));
                historyNotes.SetNotes(release?.Notes ?? UpdateService.BundledHistory(I18n.Code));
            };
            historyPanel.Controls.Add(historyBody);
            historyPanel.Controls.Add(versions);
            content.Controls.Add(latestPanel);
            content.Controls.Add(historyPanel);
            layout.Controls.Add(content, 0, 3);
            versions.SelectedIndex = 0;
            var consent = new Label { Tag = "muted", Text = I18n.T("consent_windows"),
                Font = Theme.SmallFont, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 4) };
            layout.Controls.Add(consent, 0, 4);
            progress = new UpdateProgress { Dock = DockStyle.Fill, Visible = false, Margin = new Padding(0, 2, 0, 2) };
            layout.Controls.Add(progress, 0, 5);
            progressText = new Label { Tag = "muted", Font = Theme.SmallFont, TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4) };
            layout.Controls.Add(progressText, 0, 6);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
            install = new RoundedButton(primary: true) { Text = I18n.T("install"), Enabled = false, Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            install.Click += async (_, __) => await InstallAsync();
            check = new RoundedButton { Text = I18n.T("check_again"), Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0) };
            check.Click += async (_, __) => await RefreshAsync();
            var close = new RoundedButton { Text = I18n.T("close_cancel"), DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            actions.Controls.Add(install, 0, 0);
            actions.Controls.Add(check, 1, 0);
            actions.Controls.Add(close, 2, 0);
            layout.Controls.Add(actions, 0, 7);
            Controls.Add(layout);
            CancelButton = close;
            SelectPage(history);
            ShowLatest();
            ApplyTheme();
            themeTimer = new System.Windows.Forms.Timer { Interval = 750 };
            themeTimer.Tick += (_, __) => { Theme.Refresh(); if (paletteKey != Theme.PaletteKey) ApplyTheme(); };
            themeTimer.Start();
        }
        private void ApplyTheme() { Theme.Apply(this); paletteKey = Theme.PaletteKey; }
        private void SelectPage(bool history)
        {
            latestPanel.Visible = !history;
            historyPanel.Visible = history;
            latestTab.Selected = !history;
            historyTab.Selected = history;
            latestTab.Invalidate(); historyTab.Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }
        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height - 24) Height = area.Height - 24;
            if (Width > area.Width - 24) Width = area.Width - 24;
            ApplyTheme();
            if (checkOnShow) await RefreshAsync();
        }
        private void ShowLatest()
        {
            var release = service.State.Latest;
            displayedRelease = release;
            bool newer = UpdateClient.IsStable(release) && release.Version > UpdateClient.CurrentVersion;
            description.Text = I18n.T("installed", "\u200e" + UpdateClient.CurrentVersion.ToString(3))
                + (newer ? "     •     " + I18n.T("new_version", "\u200e" + release.Version.ToString(3)) : "");
            latestNotes.SetNotes(release?.Notes ?? UpdateService.BundledHistory(I18n.Code));
            install.Enabled = newer && !working && !installing;
            install.Text = I18n.T("install") + (newer ? " " + release.Version.ToString(3) : "");
        }
        private async Task RefreshAsync()
        {
            if (working || installing) return;
            working = true;
            check.Enabled = install.Enabled = false;
            progressText.Text = I18n.T("checking");
            try
            {
                await service.CheckAsync(cancellation.Token);
                if (IsDisposed) return;
                ShowLatest();
                progressText.Text = service.State.Latest != null && service.State.Latest.Version > UpdateClient.CurrentVersion
                    ? I18n.T("update_ready") : I18n.T("up_to_date");
                var releases = await UpdateClient.GetHistoryAsync(cancellation.Token);
                if (IsDisposed) return;
                versions.Items.Clear();
                versions.Items.Add(I18n.T("bundled_history"));
                foreach (var release in releases) versions.Items.Add(release);
                versions.SelectedIndex = versions.Items.Count > 1 ? 1 : 0;
            }
            catch (Exception ex) when (ExpectedFailure(ex))
            {
                if (!IsDisposed) progressText.Text = I18n.T("connection_error");
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
            var approvedRelease = displayedRelease;
            progress.Value = 0; progress.Visible = true;
            progressText.Text = I18n.T("downloading");
            try
            {
                var file = await UpdateClient.DownloadAsync(approvedRelease, new Progress<int>(value =>
                {
                    if (!IsDisposed && !installing) { progress.Value = value; progressText.Text = I18n.T("download_percent", value); }
                }), cancellation.Token);
                if (IsDisposed) return;
                UpdateClient.Install(file);
                installing = true;
                progressText.Text = I18n.T("install_started");
            }
            catch (Exception ex) when (ExpectedFailure(ex) || ex is Win32Exception || ex is InvalidOperationException)
            {
                if (!IsDisposed) progressText.Text = I18n.T("download_error");
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
            if (disposing && !IsDisposed) { themeTimer?.Dispose(); cancellation.Cancel(); cancellation.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
