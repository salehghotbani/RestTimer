using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace RestTimer
{
    internal static class I18n
    {
        [DataContract]
        private sealed class Catalog
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "direction")] public string Direction { get; set; }
            [DataMember(Name = "strings")] public Dictionary<string, string> Strings { get; set; }
        }
        public static readonly string[] SupportedCodes = { "en", "fa", "ar", "zh", "es", "fr", "de", "pt", "ru", "ja", "ko", "hi", "tr" };
        private static readonly Dictionary<string, Catalog> catalogs = new Dictionary<string, Catalog>();
        public static string Code { get; private set; } = Resolve("auto", CultureInfo.CurrentUICulture.Name);
        public static bool IsRtl => Code == "fa" || Code == "ar";
        public static RightToLeft Flow => IsRtl ? RightToLeft.Yes : RightToLeft.No;
        public static MessageBoxOptions MessageOptions => IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0;

        public static string Normalize(string language)
        {
            string code = (language ?? "").Replace('_', '-').Split('-')[0].ToLowerInvariant();
            return Array.IndexOf(SupportedCodes, code) >= 0 ? code : null;
        }
        public static string Preference(string value) => value == "auto" ? "auto" : Normalize(value) ?? "auto";
        public static string Resolve(string preference, string systemLanguage) => Preference(preference) == "auto"
            ? Normalize(systemLanguage) ?? "en" : Normalize(preference);
        public static void Use(string preference) => Code = Resolve(preference, CultureInfo.CurrentUICulture.Name);

        private static Catalog Read(string code)
        {
            lock (catalogs)
            {
                if (catalogs.TryGetValue(code, out var existing)) return existing;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RestTimer.Locales." + code + ".json"))
                {
                    if (stream == null) throw new InvalidDataException("Missing language catalog: " + code);
                    var serializer = new DataContractJsonSerializer(typeof(Catalog), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                    var catalog = (Catalog)serializer.ReadObject(stream);
                    catalogs.Add(code, catalog);
                    return catalog;
                }
            }
        }
        public static string NativeName(string code) => Read(code).Name;
        public static string T(string key, params object[] arguments)
        {
            if (!Read(Code).Strings.TryGetValue(key, out string value) && !Read("en").Strings.TryGetValue(key, out value))
                throw new InvalidDataException("Unknown translation key: " + key);
            return arguments.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, arguments);
        }
    }
}
