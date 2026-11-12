using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace RestTimer
{
    internal sealed class UpdateDialog : Form
    {
        private readonly Label description;
        private readonly RoundedButton action;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Version availableVersion;
        private bool checking;

        public UpdateDialog(Icon icon)
        {
            Text = "به‌روزرسانی — RestTimer";
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
            ClientSize = new Size(440, 266);

            var heading = new Label
            {
                Text = "به‌روزرسانی برنامه",
                Font = Theme.HeadingFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(24, 22, 392, 36)
            };
            var version = new Label
            {
                Text = "نسخهٔ نصب‌شده  \u200e" + UpdateClient.CurrentVersion.ToString(3),
                ForeColor = Theme.Mint,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(24, 64, 392, 26)
            };
            description = new Label
            {
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(24, 100, 392, 78)
            };
            action = new RoundedButton(primary: true)
            {
                Text = "بررسی به‌روزرسانی",
                Bounds = new Rectangle(210, 204, 206, 38)
            };
            action.Click += async (_, __) =>
            {
                if (availableVersion == null)
                    await CheckAsync();
                else
                    OpenDownloadPage();
            };
            var close = new RoundedButton
            {
                Text = "بستن",
                DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(24, 204, 100, 38)
            };
            Controls.AddRange(new Control[] { heading, version, description, action, close });
            AcceptButton = action;
            CancelButton = close;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowFrame(Handle);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await CheckAsync();
        }

        private async Task CheckAsync()
        {
            if (checking)
                return;

            checking = true;
            availableVersion = null;
            action.Enabled = false;
            action.Text = "در حال بررسی…";
            description.Text = "در حال دریافت اطلاعات آخرین نسخه از GitHub…";
            try
            {
                Version latest = await UpdateClient.GetLatestAsync(cancellation.Token);
                if (IsDisposed)
                    return;

                if (latest == null)
                    description.Text = "هنوز نسخه‌ای برای دریافت منتشر نشده است.";
                else if (latest <= UpdateClient.CurrentVersion)
                    description.Text = "برنامهٔ شما به‌روز است.";
                else
                {
                    availableVersion = latest;
                    description.Text = "نسخهٔ \u200e" + latest.ToString(3)
                        + " آمادهٔ دریافت است. فایل ZIP نسخهٔ جدید را دانلود کن، برنامه را ببند و فایل‌ها را جایگزین کن.";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException
                || ex is IOException || ex is SerializationException || ex is XmlException)
            {
                if (!IsDisposed)
                    description.Text = "بررسی نسخه ممکن نشد. اتصال اینترنت را بررسی کن و دوباره تلاش کن.";
            }
            finally
            {
                checking = false;
                if (!IsDisposed)
                {
                    action.Enabled = true;
                    action.Text = availableVersion == null ? "بررسی دوباره" : "دریافت نسخهٔ جدید";
                }
            }
        }

        private void OpenDownloadPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(UpdateClient.ReleasesUrl) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                description.Text = "باز کردن مرورگر ممکن نشد. صفحهٔ Releases مخزن RestTimer در GitHub را باز کن.";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
