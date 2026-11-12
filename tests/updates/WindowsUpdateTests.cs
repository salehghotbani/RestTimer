using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RestTimer;

internal static class WindowsUpdateTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Invalid release was accepted.");
    }
    private sealed class Handler : HttpMessageHandler
    {
        public byte[] Bytes = new byte[] { 1, 2, 3, 4, 5 };
        public long? Length;
        public CancellationTokenSource Cancel;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (Cancel != null) Cancel.Cancel();
            var content = new ByteArrayContent(Bytes);
            content.Headers.ContentLength = Length ?? Bytes.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("Windows updater: daily/retry persistence, release filtering, trusted assets, verified download, corruption, truncation and cancellation passed."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "RestTimer-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            DateTime now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
            var schedule = new UpdateSchedule();
            Assert(schedule.IsDue(now), "First check is due.");
            schedule.Attempt(now);
            Assert(!schedule.IsDue(now.AddHours(5)) && schedule.IsDue(now.AddHours(6)), "Failure must retry after six hours.");
            schedule.Success(now);
            Assert(!schedule.IsDue(now.AddHours(23)) && schedule.IsDue(now.AddHours(24)), "Daily check interval.");
            Assert(schedule.IsDue(now.AddDays(-30)), "Clock change postponed checks.");
            schedule.DailyEnabled = false;
            Assert(!schedule.IsDue(now.AddDays(2)), "Opt-out ignored.");

            var release = UpdateClient.ParseRelease("{\"tag_name\":\"v9.0.0\",\"body\":\"تغییرات نسخه\",\"assets\":[]}");
            Assert(release.Version > UpdateClient.CurrentVersion && release.Notes.Contains("تغییرات"), "Notes/version parsing failed.");
            Assert(UpdateClient.ParseRelease("{\"tag_name\":\"v99.0.0\",\"prerelease\":true}") == null, "Prerelease accepted.");
            Assert(UpdateClient.ParseRelease("{\"tag_name\":\"v99.0.0\",\"draft\":true}") == null, "Draft accepted.");
            Reject(() => UpdateClient.ParseVersion("v1.0.0/../../bad"));
            Reject(() => UpdateClient.GetInstaller(release));
            var service = new UpdateService(root);
            service.State.DailyEnabled = false;
            service.State.Success(now);
            service.State.Latest = release;
            service.State.NotifiedTag = release.Tag;
            service.Save();
            var restored = new UpdateService(root);
            Assert(!restored.State.DailyEnabled && restored.State.NotifiedTag == release.Tag
                && restored.State.Latest.Notes == release.Notes && restored.State.LastCheckedUtc == service.State.LastCheckedUtc,
                "Schedule, notification or cached notes did not persist.");
            Assert(UpdateClient.InstallerArguments(@"C:\Program Files\RestTimer", "en").Contains("/DIR=\"C:\\Program Files\\RestTimer\""), "Installer directory is not quoted.");

            using (var handler = new Handler())
            using (var client = new HttpClient(handler))
            {
                string digest;
                using (var hash = SHA256.Create()) digest = "sha256:" + BitConverter.ToString(hash.ComputeHash(handler.Bytes)).Replace("-", "").ToLowerInvariant();
                var asset = new ReleaseAsset { Name = "RestTimer-v9.0.0-windows-x64-setup.exe", Size = 5, Digest = digest };
                asset.Url = "https://github.com/salehghotbani/RestTimer/releases/download/v9.0.0/" + asset.Name;
                release.Assets = new[] { asset };
                string url = asset.Url;
                asset.Url = "https://example.com/" + asset.Name;
                Reject(() => UpdateClient.GetInstaller(release));
                asset.Url = url;
                string path = await UpdateClient.DownloadAsync(release, null, CancellationToken.None, client, root);
                Assert(File.ReadAllBytes(path).Length == 5, "Verified download missing.");
                File.Delete(path);
                handler.Bytes = new byte[] { 5, 4, 3, 2, 1 };
                await RejectDownload(release, client, root, CancellationToken.None);
                Assert(!File.Exists(path) && Directory.GetFiles(root, "*.part").Length == 0, "Corrupt/partial file retained.");
                handler.Bytes = new byte[] { 1, 2 };
                handler.Length = 5;
                await RejectDownload(release, client, root, CancellationToken.None);
                using (var cancel = new CancellationTokenSource())
                {
                    handler.Cancel = cancel;
                    try { await UpdateClient.DownloadAsync(release, null, cancel.Token, client, root); throw new Exception("Cancellation ignored."); }
                    catch (OperationCanceledException) { }
                }
                Assert(Directory.GetFiles(root, "*.part").Length == 0, "Cancelled partial file retained.");
            }
        }
        finally { Directory.Delete(root, true); }
    }
    private static async Task RejectDownload(ReleaseInfo release, HttpClient client, string root, CancellationToken token)
    {
        try { await UpdateClient.DownloadAsync(release, null, token, client, root); }
        catch (InvalidDataException) { return; }
        throw new Exception("Unverified download was accepted.");
    }
}
