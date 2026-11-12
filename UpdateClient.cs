using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RestTimer
{
    [DataContract]
    internal sealed class ReleaseInfo
    {
        [DataMember(Name = "tag_name")] public string Tag { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "body")] public string Notes { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public ReleaseAsset[] Assets { get; set; }
        public Version Version => UpdateClient.ParseVersion(Tag);
        public override string ToString() => "RestTimer " + Version.ToString(3);
    }
    [DataContract]
    internal sealed class ReleaseAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string Url { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }
    }
    internal static class UpdateClient
    {
        public const string ReleasesUrl = "https://github.com/salehghotbani/RestTimer/releases/latest";
        internal const string ApiUrl = "https://api.github.com/repos/salehghotbani/RestTimer/releases";
        internal const long MaxInstallerSize = 250L * 1024 * 1024;
        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;

        internal static HttpClient CreateClient(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout, MaxResponseContentBufferSize = 8 * 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RestTimer/" + CurrentVersion.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
        public static async Task<ReleaseInfo> GetLatestAsync(CancellationToken token)
        {
            using (var client = CreateClient(TimeSpan.FromSeconds(20)))
            using (var response = await client.GetAsync(ApiUrl + "/latest", token).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                response.EnsureSuccessStatusCode();
                return ParseRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
        public static async Task<List<ReleaseInfo>> GetHistoryAsync(CancellationToken token)
        {
            var releases = new List<ReleaseInfo>();
            using (var client = CreateClient(TimeSpan.FromSeconds(20)))
            {
                for (int page = 1; ; page++)
                {
                    using (var response = await client.GetAsync(ApiUrl + "?per_page=100&page=" + page, token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        var batch = Deserialize<ReleaseInfo[]>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        if (batch == null) throw new InvalidDataException("Invalid release history.");
                        releases.AddRange(batch.Where(IsStable));
                        if (batch.Length < 100) break;
                    }
                }
            }
            return releases.OrderByDescending(release => release.Version).ToList();
        }
        internal static Version ParseVersion(string tag)
        {
            if (tag == null || !Regex.IsMatch(tag, @"\A[vV]?\d+\.\d+(?:\.\d+){0,2}\z")
                || !Version.TryParse(tag.TrimStart('v', 'V'), out Version version))
                throw new InvalidDataException("Unsupported release version.");
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }
        internal static bool IsStable(ReleaseInfo release)
        {
            if (release == null || release.Draft || release.Prerelease) return false;
            try { ParseVersion(release.Tag); return true; }
            catch (InvalidDataException) { return false; }
        }
        internal static ReleaseInfo ParseRelease(string json)
        {
            var release = Deserialize<ReleaseInfo>(json);
            if (release == null || release.Draft || release.Prerelease) return null;
            ParseVersion(release.Tag);
            return release;
        }
        internal static T Deserialize<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        internal static ReleaseAsset GetInstaller(ReleaseInfo release)
        {
            if (!IsStable(release)) throw new InvalidDataException("Not a stable release.");
            string name = "RestTimer-v" + release.Version.ToString(3) + "-windows-x64-setup.exe";
            var asset = release.Assets?.SingleOrDefault(item => item?.Name == name);
            string expected = "https://github.com/salehghotbani/RestTimer/releases/download/" + release.Tag + "/" + name;
            if (asset == null || asset.Url != expected || asset.Size < 1 || asset.Size > MaxInstallerSize
                || asset.Digest == null || !Regex.IsMatch(asset.Digest, @"\Asha256:[a-fA-F0-9]{64}\z"))
                throw new InvalidDataException("A verified Windows installer is not available.");
            return asset;
        }
        public static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<int> progress,
            CancellationToken token, HttpClient suppliedClient = null, string directory = null)
        {
            var asset = GetInstaller(release);
            directory = directory ?? Path.Combine(UpdateService.DataDirectory, "downloads");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, asset.Name);
            string partial = target + "." + Guid.NewGuid().ToString("N") + ".part";
            var client = suppliedClient ?? CreateClient(TimeSpan.FromMinutes(30));
            try
            {
                using (var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength != asset.Size)
                        throw new InvalidDataException("Installer size mismatch.");
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    using (var hash = SHA256.Create())
                    {
                        var buffer = new byte[81920];
                        long total = 0;
                        int count;
                        while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                        {
                            total += count;
                            if (total > asset.Size) throw new InvalidDataException("Installer exceeds expected size.");
                            await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                            hash.TransformBlock(buffer, 0, count, null, 0);
                            progress?.Report((int)(total * 100 / asset.Size));
                        }
                        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        string digest = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                        if (total != asset.Size || !string.Equals("sha256:" + digest, asset.Digest, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Installer verification failed.");
                    }
                }
                token.ThrowIfCancellationRequested();
                if (File.Exists(target)) File.Delete(target);
                File.Move(partial, target);
                return target;
            }
            finally
            {
                if (suppliedClient == null) client.Dispose();
                if (File.Exists(partial)) File.Delete(partial);
            }
        }
        internal static string InstallerArguments(string directory, string language)
        {
            if (directory.Contains("\"") || (language != "en" && language != "fa")) throw new ArgumentException();
            return "/SILENT /NORESTART /LANG=" + language + " /DIR=\"" + directory + "\"";
        }
        internal static void Install(string path)
        {
            string language;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{77527277-48F0-4BDF-B74C-99A026A3B8C5}_is1"))
                language = key?.GetValue("Inno Setup: Language") as string;
            var info = new ProcessStartInfo(path, InstallerArguments(
                System.Windows.Forms.Application.StartupPath, language == "en" ? "en" : "fa")) { UseShellExecute = true };
            Process.Start(info)?.Dispose();
        }
    }
}
