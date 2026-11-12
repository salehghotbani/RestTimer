param([switch]$TestPersistence)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    New-Item -ItemType Directory -Path obj/localization -Force | Out-Null
    @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
internal static class LocalizationSmoke
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var i18n = app.GetType("RestTimer.I18n", true);
            var use = i18n.GetMethod("Use");
            var resolve = i18n.GetMethod("Resolve");
            var translate = i18n.GetMethod("T");
            var codes = (string[])i18n.GetField("SupportedCodes").GetValue(null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var sample in new[] { new[] {"fa-IR","fa"}, new[] {"pt-BR","pt"}, new[] {"zh-TW","zh"}, new[] {"he-IL","en"}, new[] {"xx","en"} })
                if ((string)resolve.Invoke(null, new object[] { "auto", sample[0] }) != sample[1]
                    || (string)resolve.Invoke(null, new object[] { "de", sample[0] }) != "de")
                    throw new Exception("Locale resolution or user override failed.");
            var culture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fa-IR");
                use.Invoke(null, new object[] { "auto" });
                if ((string)i18n.GetProperty("Code").GetValue(null) != "fa") throw new Exception("System UI language was ignored.");
                use.Invoke(null, new object[] { "en" });
                if ((string)i18n.GetProperty("Code").GetValue(null) != "en") throw new Exception("Manual language did not override system UI language.");
            }
            finally { CultureInfo.CurrentUICulture = culture; }
            var preferences = app.GetType("RestTimer.Preferences", true);
            var settings = app.GetType("RestTimer.SettingsDialog", true);
            var updater = app.GetType("RestTimer.UpdateService", true);
            var service = Activator.CreateInstance(updater, new object[] { System.IO.Path.GetFullPath("obj/localization/updates") });
            var theme = app.GetType("RestTimer.Theme", true);
            var light = theme.GetProperty("LightOverride", BindingFlags.Static | BindingFlags.NonPublic);
            var contextType = app.GetType("RestTimer.RestTimerContext", true);
            using (var context = (ApplicationContext)Activator.CreateInstance(contextType, new object[] { false, false }))
            {
            var clock = (System.Diagnostics.Stopwatch)contextType.GetField("clock", flags).GetValue(context);
            clock.Stop();
            var frozen = clock.Elapsed;
            foreach (string code in codes)
            {
                if (code == "he") throw new Exception("Unexpected language.");
                use.Invoke(null, new object[] { code });
                var selected = Activator.CreateInstance(preferences, new object[] { 3723, 901, false, code });
                contextType.GetField("preferences", flags).SetValue(context, selected);
                contextType.GetMethod("ApplyLanguage", flags).Invoke(context, null);
                var menu = (ContextMenuStrip)contextType.GetField("menu", flags).GetValue(context);
                if (clock.IsRunning || clock.Elapsed != frozen || menu.Items[0].Text != (string)translate.Invoke(null, new object[] { "start", new object[0] }))
                    throw new Exception("Applying a language reset the paused timer or failed to translate the menu.");
                if ((string)preferences.GetProperty("Language").GetValue(selected) != code) throw new Exception("Language preference was not retained.");
                foreach (bool mode in new[] { true, false })
                {
                    light.SetValue(null, mode);
                    theme.GetMethod("Refresh").Invoke(null, null);
                    using (var dialog = (Form)Activator.CreateInstance(settings, selected, SystemIcons.Application, (Action)(() => {}), (Action)(() => {})))
                    {
                        settings.GetMethod("SetRunning").Invoke(dialog, new object[] { false });
                        dialog.Show(); Application.DoEvents();
                        var combo = (ComboBox)settings.GetField("language", flags).GetValue(dialog);
                        if (combo.Items.Count != codes.Length + 1 || combo.SelectedIndex < 1
                            || dialog.RightToLeft != (code == "fa" || code == "ar" ? RightToLeft.Yes : RightToLeft.No))
                            throw new Exception("Language selector or layout direction failed for " + code);
                        settings.GetMethod("SaveDurations", flags).Invoke(dialog, null);
                        var saved = settings.GetProperty("SelectedPreferences").GetValue(dialog);
                        if ((int)preferences.GetProperty("WorkSeconds").GetValue(saved) != 3723
                            || (string)preferences.GetProperty("Language").GetValue(saved) != code)
                            throw new Exception("Saving a language changed duration or lost the language.");
                        using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                        { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size)); bitmap.Save("obj/localization/settings-"+code+"-"+(mode?"light":"dark")+".png",ImageFormat.Png); }
                    }
                    using (var dialog = (Form)Activator.CreateInstance(app.GetType("RestTimer.UpdateDialog",true),SystemIcons.Application,service,false,false))
                    {
                        dialog.Show(); Application.DoEvents();
                        using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                        { dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size)); bitmap.Save("obj/localization/updates-"+code+"-"+(mode?"light":"dark")+".png",ImageFormat.Png); }
                    }
                }
                Console.WriteLine("Localized settings, selector, direction and updates passed: " + code);
            }
            }
            if (args.Length > 1 && args[1] == "--persist")
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\RestTimer"))
                {
                    var names = new[] { "Language", "DurationsSeconds", "ShowTaskbarCountdown" };
                    var values = new object[names.Length];
                    var kinds = new Microsoft.Win32.RegistryValueKind[names.Length];
                    for (int i=0;i<names.Length;i++) { values[i]=key.GetValue(names[i]); if(values[i]!=null) kinds[i]=key.GetValueKind(names[i]); }
                    try
                    {
                        foreach (string code in new[] {"auto","fa","en","ar","zh","es","fr","de","pt","ru","ja","ko","hi","tr"})
                        {
                            var selected = Activator.CreateInstance(preferences,new object[]{3723,901,false,code});
                            preferences.GetMethod("Save").Invoke(selected,null);
                            var loaded = preferences.GetMethod("Load").Invoke(null,null);
                            if((string)preferences.GetProperty("Language").GetValue(loaded)!=code
                                ||(int)preferences.GetProperty("WorkSeconds").GetValue(loaded)!=3723
                                ||(bool)preferences.GetProperty("ShowTaskbarCountdown").GetValue(loaded))
                                throw new Exception("Language or timer preferences did not persist.");
                        }
                        Console.WriteLine("Windows registry language and timer persistence passed.");
                    }
                    finally { for(int i=0;i<names.Length;i++) { if(values[i]==null) key.DeleteValue(names[i],false); else key.SetValue(names[i],values[i],kinds[i]); } }
                }
            }
            light.SetValue(null,null);
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
'@ | Set-Content -LiteralPath obj/localization/LocalizationSmoke.cs -Encoding utf8
    $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $testSource = (Resolve-Path 'obj\localization\LocalizationSmoke.cs').Path
    $testExe = Join-Path (Get-Location).Path 'obj\localization\LocalizationSmoke.exe'
    & $compiler /nologo /debug:full /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$testExe" $testSource
    if ($LASTEXITCODE -ne 0) { throw 'Localization test compilation failed.' }
    Copy-Item dist/Windows/RestTimer.exe.config obj/localization/LocalizationSmoke.exe.config
    $testArguments = @((Resolve-Path dist/Windows/RestTimer.exe).Path)
    if ($TestPersistence) { $testArguments += '--persist' }
    & ./obj/localization/LocalizationSmoke.exe @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Windows localization tests failed.' }
} finally { Pop-Location }
