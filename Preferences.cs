using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RestTimer
{
    internal sealed class Preferences
    {
        private const string SettingsKey = @"Software\RestTimer";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "RestTimer";
        private const string DurationsName = "Durations";
        private const string DurationsSecondsName = "DurationsSeconds";
        public const int MaxWorkSeconds = 24 * 60 * 60;
        public const int MaxBreakSeconds = 2 * 60 * 60;

        public int WorkSeconds { get; }
        public int BreakSeconds { get; }
        public bool ShowTaskbarCountdown { get; }

        public Preferences(int workSeconds = 3600, int breakSeconds = 900)
            : this(workSeconds, breakSeconds, true) { }

        public Preferences(int workSeconds, int breakSeconds, bool showTaskbarCountdown)
        {
            if (workSeconds < 1 || workSeconds > MaxWorkSeconds)
                throw new ArgumentOutOfRangeException(nameof(workSeconds));
            if (breakSeconds < 1 || breakSeconds > MaxBreakSeconds)
                throw new ArgumentOutOfRangeException(nameof(breakSeconds));
            WorkSeconds = workSeconds;
            BreakSeconds = breakSeconds;
            ShowTaskbarCountdown = showTaskbarCountdown;
        }

        public static Preferences Load()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                var durations = ReadDurations(key?.GetValue(DurationsSecondsName), 1)
                    ?? ReadDurations(key?.GetValue(DurationsName), 60)
                    ?? new Preferences();
                return new Preferences(durations.WorkSeconds, durations.BreakSeconds,
                    !(key?.GetValue("ShowTaskbarCountdown") is int enabled) || enabled != 0);
            }
        }

        private static Preferences ReadDurations(object value, int secondsPerUnit)
        {
            if (value is string[] values && values.Length == 2
                && int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out int work)
                && int.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out int rest)
                && work >= 1 && work <= MaxWorkSeconds / secondsPerUnit
                && rest >= 1 && rest <= MaxBreakSeconds / secondsPerUnit)
                return new Preferences(work * secondsPerUnit, rest * secondsPerUnit);

            return null;
        }

        public void Save()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                key.SetValue("ShowTaskbarCountdown", ShowTaskbarCountdown ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(DurationsSecondsName, new[]
                {
                    WorkSeconds.ToString(CultureInfo.InvariantCulture),
                    BreakSeconds.ToString(CultureInfo.InvariantCulture)
                }, RegistryValueKind.MultiString);
            }
        }

        private static string StartupCommand => "\"" + Application.ExecutablePath + "\"";

        public static bool StartsWithWindows()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return string.Equals(key?.GetValue(AppName) as string, StartupCommand, StringComparison.OrdinalIgnoreCase);
        }

        public static void SetStartup(bool enabled)
        {
            if (enabled && StartupCommand.Length > 260)
                throw new IOException("مسیر برنامه برای اجرای خودکار طولانی است. برنامه را به مسیر کوتاه‌تری منتقل کنید.");

            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    key.SetValue(AppName, StartupCommand, RegistryValueKind.String);
                else
                    key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
