using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestTimer
{
    internal static class UpdateClient
    {
        public const string ReleasesUrl = "https://github.com/salehghotbani/RestTimer/releases/latest";
        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public static async Task<Version> GetLatestAsync(CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RestTimer/" + CurrentVersion.ToString(3));
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                using (var response = await client.GetAsync(
                    "https://api.github.com/repos/salehghotbani/RestTimer/releases/latest", cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return null;

                    response.EnsureSuccessStatusCode();
                    return ParseRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
        }

        internal static Version ParseRelease(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(Release));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var release = (Release)serializer.ReadObject(stream);
                if (release == null || release.Draft || release.Prerelease)
                    return null;

                string tag = release.TagName;
                if (tag != null && tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    tag = tag.Substring(1);
                if (!Version.TryParse(tag, out Version version))
                    throw new InvalidDataException("The release tag is not a supported version.");

                return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
            }
        }

        [DataContract]
        private sealed class Release
        {
            [DataMember(Name = "tag_name")]
            public string TagName { get; set; }
            [DataMember(Name = "draft")]
            public bool Draft { get; set; }
            [DataMember(Name = "prerelease")]
            public bool Prerelease { get; set; }
        }
    }
}
