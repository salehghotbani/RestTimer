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
        public const int MaxWorkMinutes = 1440;
        public const int MaxBreakMinutes = 120;

        public int WorkMinutes { get; }
        public int BreakMinutes { get; }

        public Preferences(int workMinutes = 60, int breakMinutes = 15)
        {
            WorkMinutes = workMinutes;
            BreakMinutes = breakMinutes;
        }

        public static Preferences Load()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                if (key?.GetValue(DurationsName) is string[] values && values.Length == 2
                    && int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out int work)
                    && int.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out int rest)
                    && work >= 1 && work <= MaxWorkMinutes && rest >= 1 && rest <= MaxBreakMinutes)
                    return new Preferences(work, rest);
            }

            return new Preferences();
        }

        public void Save()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                key.SetValue(DurationsName, new[]
                {
                    WorkMinutes.ToString(CultureInfo.InvariantCulture),
                    BreakMinutes.ToString(CultureInfo.InvariantCulture)
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
