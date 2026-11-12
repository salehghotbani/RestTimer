using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RestTimer
{
    [DataContract]
    internal sealed class UpdateSchedule
    {
        [DataMember] public bool DailyEnabled { get; set; } = true;
        [DataMember] public string NextAttemptUtc { get; set; }
        [DataMember] public string LastCheckedUtc { get; set; }
        [DataMember] public string NotifiedTag { get; set; }
        [DataMember] public ReleaseInfo Latest { get; set; }
        public bool IsDue(DateTime utcNow)
        {
            if (!DailyEnabled) return false;
            if (!DateTime.TryParse(NextAttemptUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime next)) return true;
            return utcNow >= next || next - utcNow > TimeSpan.FromDays(1);
        }
        public void Attempt(DateTime now) => NextAttemptUtc = now.AddHours(6).ToString("o", CultureInfo.InvariantCulture);
        public void Success(DateTime now)
        {
            LastCheckedUtc = now.ToString("o", CultureInfo.InvariantCulture);
            NextAttemptUtc = now.AddDays(1).ToString("o", CultureInfo.InvariantCulture);
        }
    }
    internal sealed class UpdateService
    {
        public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestTimer", "updates");
        private readonly string stateFile;
        public UpdateSchedule State { get; private set; }
        private Task<ReleaseInfo> pending;
        public bool Checking => pending != null && !pending.IsCompleted;
        public UpdateService(string directory = null)
        {
            stateFile = Path.Combine(directory ?? DataDirectory, "state.json");
            State = new UpdateSchedule();
            try
            {
                if (File.Exists(stateFile)) State = UpdateClient.Deserialize<UpdateSchedule>(File.ReadAllText(stateFile)) ?? State;
                if (!UpdateClient.IsStable(State.Latest)) State.Latest = null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException) { }
        }
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(stateFile));
                string temporary = stateFile + ".tmp";
                using (var stream = File.Create(temporary)) new DataContractJsonSerializer(typeof(UpdateSchedule)).WriteObject(stream, State);
                if (File.Exists(stateFile)) File.Replace(temporary, stateFile, null);
                else File.Move(temporary, stateFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }
        public Task<ReleaseInfo> CheckAsync(CancellationToken token)
        {
            if (!Checking) pending = CheckCoreAsync(token);
            return pending;
        }

        private async Task<ReleaseInfo> CheckCoreAsync(CancellationToken token)
        {
            State.Attempt(DateTime.UtcNow);
            Save();
            var release = await UpdateClient.GetLatestAsync(token).ConfigureAwait(false);
            State.Latest = release;
            State.Success(DateTime.UtcNow);
            Save();
            return release;
        }
        public static string BundledHistory()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RestTimer.CHANGELOG.md"))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
}
