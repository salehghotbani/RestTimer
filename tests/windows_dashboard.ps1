$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    New-Item -ItemType Directory -Force obj/dashboard | Out-Null
    @'
using System;
using System.Drawing;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
class DashboardSmoke
{
    [STAThread] static int Main(string[] args)
    {
        try {
            Application.EnableVisualStyles();
            var app=Assembly.LoadFrom(args[0]);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var entry=app.GetType("RestTimer.WorkEntry",true);
            var journal=app.GetType("RestTimer.WorkJournal",true);
            var i18n=app.GetType("RestTimer.I18n",true);
            var reportType=app.GetType("RestTimer.WorkReportPanel",true);
            var theme=app.GetType("RestTimer.Theme",true);
            var start=new DateTimeOffset(DateTime.Today.AddHours(8).AddMinutes(30));
            var raw=Array.CreateInstance(entry,3);
            string[] names={"نوشتن گزارش","Development","Reading"};
            double[] lengths={5400,3600,1800};
            int[] hours={8,11,14};
            for(int i=0;i<3;i++) {
                var row=Activator.CreateInstance(entry);
                entry.GetField("Task").SetValue(row,names[i]);entry.GetField("Seconds").SetValue(row,lengths[i]);
                entry.GetField("StartedAt").SetValue(row,(i==0?start:new DateTimeOffset(DateTime.Today.AddHours(hours[i]))).ToString("o"));
                raw.SetValue(row,i);
            }
            var hourly=(double[])journal.GetMethod("Hourly").Invoke(null,new object[]{raw});
            if(hourly[8]!=1800 || hourly[9]!=3600 || hourly[11]!=3600 || hourly[14]!=1800 || hourly.Sum()!=10800) throw new Exception("Hourly allocation failed.");
            foreach(string code in new[]{"en","fa"}) {
                i18n.GetMethod("Use").Invoke(null,new object[]{code});
                using(var form=new Form{Size=new Size(1100,900)})
                using(var report=(Control)Activator.CreateInstance(reportType)) {
                    form.Controls.Add(report);form.Show();Application.DoEvents();
                    reportType.GetField("entries",flags).SetValue(report,raw);
                    var chart=reportType.GetField("chart",flags).GetValue(report);
                    chart.GetType().GetField("Entries").SetValue(chart,raw);
                    var modes=(ComboBox)reportType.GetField("chartType",flags).GetValue(report);
                    for(int mode=0;mode<3;mode++) {
                        modes.SelectedIndex=mode;
                        foreach(bool light in new[]{false,true}) {
                            theme.GetProperty("LightOverride",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,light);
                            theme.GetMethod("Refresh").Invoke(null,null);
                            theme.GetMethod("Apply").Invoke(null,new object[]{form});Application.DoEvents();
                            using(var image=new Bitmap(report.Width,report.Height)) {
                                report.DrawToBitmap(image,new Rectangle(Point.Empty,report.Size));
                                image.Save("obj/dashboard/ui-"+code+"-"+mode+"-"+(light?"light":"dark")+".png");
                            }
                        }
                        reportType.GetMethod("Export").Invoke(report,new object[]{Path.GetFullPath("obj/dashboard/"+code+"-"+mode+".png"),false});
                        reportType.GetMethod("Export").Invoke(report,new object[]{Path.GetFullPath("obj/dashboard/"+code+"-"+mode+".pdf"),true});
                    }
                    var many=Array.CreateInstance(entry,55);
                    for(int i=0;i<55;i++) {var row=Activator.CreateInstance(entry);entry.GetField("Task").SetValue(row,"Task "+i.ToString("00"));entry.GetField("Seconds").SetValue(row,60.0*(i+1));many.SetValue(row,i);}
                    reportType.GetField("entries",flags).SetValue(report,many);
                    reportType.GetMethod("Export").Invoke(report,new object[]{Path.GetFullPath("obj/dashboard/many-"+code+".pdf"),true});
                    reportType.GetField("entries",flags).SetValue(report,Array.CreateInstance(entry,0));
                    reportType.GetMethod("Export").Invoke(report,new object[]{Path.GetFullPath("obj/dashboard/empty-"+code+".pdf"),true});
                }
            }
            Console.WriteLine("Hourly chart data, all 3 charts, light/dark RTL, PNG/PDF and multipage export passed.");
            return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
'@ | Set-Content -Encoding UTF8 obj/dashboard/DashboardSmoke.cs
    $testSource=(Resolve-Path 'obj/dashboard/DashboardSmoke.cs').Path
    $testExe=Join-Path (Get-Location).Path 'obj/dashboard/DashboardSmoke.exe'
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$testExe" $testSource
    if($LASTEXITCODE -ne 0) {throw 'Dashboard test compilation failed.'}
    Copy-Item dist/Windows/RestTimer.exe.config obj/dashboard/DashboardSmoke.exe.config
    & ./obj/dashboard/DashboardSmoke.exe (Resolve-Path dist/Windows/RestTimer.exe).Path
    if($LASTEXITCODE -ne 0) {throw 'Dashboard tests failed.'}
} finally {Pop-Location}
