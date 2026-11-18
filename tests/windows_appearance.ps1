$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    New-Item -ItemType Directory -Path obj/appearance -Force | Out-Null
    @'
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class AppearanceTest {
 [STAThread] static int Main() {
  try {
   Application.EnableVisualStyles();
   var app=Assembly.LoadFrom(System.IO.Path.GetFullPath("dist/Windows/RestTimer.exe"));
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var dates=app.GetType("RestTimer.DateDisplay");var settings=app.GetType("RestTimer.SettingsDialog");var theme=app.GetType("RestTimer.Theme");
   foreach(string language in new[]{"en","fa","ar"}) foreach(string mode in new[]{"light","dark"}) {
    app.GetType("RestTimer.I18n").GetMethod("Use").Invoke(null,new object[]{language});
    theme.GetMethod("Use").Invoke(null,new object[]{mode});
    var prefs=Activator.CreateInstance(app.GetType("RestTimer.Preferences"),new object[]{3600,900,true,language,"auto",mode});
    var updater=Activator.CreateInstance(app.GetType("RestTimer.UpdateService"),new object[]{System.IO.Path.GetFullPath("obj/appearance/updates")});
    using(var dialog=(Form)Activator.CreateInstance(settings,prefs,SystemIcons.Application,(Action)(()=>{}),(Action)(()=>{}),updater,"general",false)) {
     dialog.Size=new Size(900,680);dialog.Show();Application.DoEvents();
     var body=(Panel)settings.GetField("generalBody",flags).GetValue(dialog);
     foreach(string group in new[]{"time_settings","language_region","appearance","app_behavior"}) {
      var section=body.Controls["section:"+group];
      if(section==null || section.Controls.Cast<Control>().Any(c=>c.Visible && !section.ClientRectangle.Contains(c.Bounds))) throw new Exception("Settings section is clipped: "+group);
     }
     foreach(var pair in new[]{new[]{"workDuration","time_settings"},new[]{"startTimer","time_settings"},new[]{"resetTimer","time_settings"},new[]{"language","language_region"},new[]{"calendarChoice","language_region"},new[]{"themeChoice","appearance"},new[]{"daily","app_behavior"},new[]{"startupPreference","app_behavior"}}) {
      var control=(Control)settings.GetField(pair[0],flags).GetValue(dialog);
      if(control.Parent.Name!="section:"+pair[1]) throw new Exception("Setting is in the wrong section: "+pair[0]);
     }
     if(body.AutoScroll || body.Controls.Cast<Control>().Any(c=>c.Visible && !body.ClientRectangle.Contains(c.Bounds))) throw new Exception("Small settings must fit: "+language+" "+body.ClientSize);
     using(var image=new Bitmap(dialog.Width,dialog.Height)) {dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save("obj/appearance/general-"+language+"-"+mode+".png");}
     var report=settings.GetField("reportPage",flags).GetValue(dialog);var picker=report.GetType().GetField("day",flags).GetValue(report);var pickerType=picker.GetType();
     pickerType.GetProperty("Value").SetValue(picker,new DateTime(2024,3,20));
     foreach(string kind in new[]{"gregorian","persian","hijri"}) {
      var box=(ComboBox)settings.GetField("calendarChoice",flags).GetValue(dialog);box.SelectedIndex=Array.IndexOf(new[]{"auto","gregorian","persian","hijri"},kind);
      if((DateTime)pickerType.GetProperty("Value").GetValue(picker)!=new DateTime(2024,3,20)) throw new Exception("Calendar changed date identity.");
      var cal=(System.Globalization.Calendar)dates.GetProperty("Calendar").GetValue(null);
      if(cal.GetYear(new DateTime(2024,3,20))!=(kind=="persian"?1403:kind=="hijri"?1445:2024)) throw new Exception("Wrong calendar year.");
      if(kind=="persian" && (cal.GetMonth(new DateTime(2024,3,20))!=1 || cal.GetDayOfMonth(new DateTime(2024,3,20))!=1)) throw new Exception("Nowruz conversion failed.");
      DateTime chosen=DateTime.MinValue;
      using(var grid=(Control)Activator.CreateInstance(app.GetType("RestTimer.CalendarGrid"),new DateTime(2024,3,20),(Action<DateTime>)(d=>chosen=d)))
      using(var host=new Form {ClientSize=grid.Size}) {
       grid.Dock=DockStyle.Fill;host.Controls.Add(grid);host.Show();Application.DoEvents();
       using(var image=new Bitmap(grid.Width,grid.Height)){grid.DrawToBitmap(image,grid.ClientRectangle);image.Save("obj/appearance/calendar-"+language+"-"+kind+"-"+mode+".png");}
       // The selected day's button must select that exact Gregorian date.
       var table=(TableLayoutPanel)grid.GetType().GetField("days",flags).GetValue(grid);
       var selected=table.Controls.Cast<Button>().Single(b=>(bool)b.GetType().GetProperty("Selected").GetValue(b));selected.PerformClick();
       if(chosen!=new DateTime(2024,3,20)) throw new Exception("Calendar click selected another date.");
       host.Close();
      }
      settings.GetMethod("SelectPage").Invoke(dialog,new object[]{"report"});
      pickerType.GetMethod("OpenCalendar").Invoke(picker,null);Application.DoEvents();
      var popup=(ToolStripDropDown)pickerType.GetField("popup",flags).GetValue(picker);
      if(!popup.Visible || !Screen.FromControl(dialog).WorkingArea.Contains(popup.Bounds)) throw new Exception("Calendar popup is not visible within the screen.");
      var hosted=popup.Items.OfType<ToolStripControlHost>().Single().Control;
      var monthBox=(ComboBox)hosted.GetType().GetField("months",flags).GetValue(hosted);
      monthBox.DroppedDown=true;Application.DoEvents();monthBox.DroppedDown=false;Application.DoEvents();
      if(!popup.Visible) throw new Exception("Month selection dismissed the calendar.");
      var hostedDays=(TableLayoutPanel)hosted.GetType().GetField("days",flags).GetValue(hosted);
      hostedDays.Controls.Cast<Button>().Single(b=>(bool)b.GetType().GetProperty("Selected").GetValue(b)).PerformClick();Application.DoEvents();
      if(popup.Visible || (DateTime)pickerType.GetProperty("Value").GetValue(picker)!=new DateTime(2024,3,20)) throw new Exception("Popup date selection failed.");
     }
     var themes=(ComboBox)settings.GetField("themeChoice",flags).GetValue(dialog);themes.SelectedIndex=mode=="light"?2:1;
     if((bool)theme.GetProperty("IsLight").GetValue(null)!=(mode!="light")) throw new Exception("Theme did not preview immediately.");
     var languages=(ComboBox)settings.GetField("language",flags).GetValue(dialog);
     for(int i=1;i<languages.Items.Count;i++) {languages.SelectedIndex=i;Application.DoEvents();}
     if(body.AutoScroll || body.Controls.Cast<Control>().Any(c=>c.Visible && !body.ClientRectangle.Contains(c.Bounds))) throw new Exception("Live language changes broke compact layout.");
     dialog.Dispose();
    }
   }
   Console.WriteLine("Compact layout, calendar conversion/date selection and immediate theme previews passed.");return 0;
  } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
 }
}
'@ | Set-Content -Encoding utf8 obj/appearance/AppearanceTest.cs
    $source=(Resolve-Path 'obj/appearance/AppearanceTest.cs').Path
    $testExe=Join-Path (Get-Location) 'obj/appearance/AppearanceTest.exe'
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$testExe" $source
    if($LASTEXITCODE -ne 0) {throw 'Appearance test compilation failed.'}
    Copy-Item dist/Windows/RestTimer.exe.config obj/appearance/AppearanceTest.exe.config
    & ./obj/appearance/AppearanceTest.exe
    if($LASTEXITCODE -ne 0) {throw 'Appearance tests failed.'}
} finally {Pop-Location}
