using System;
using System.Threading;
using System.Windows.Forms;

namespace RestTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var instance = new Mutex(false, @"Local\RestTimer.Singleton", out bool firstInstance))
            {
                if (!firstInstance)
                    return;

                using (var context = new RestTimerContext())
                    Application.Run(context);
            }
        }
    }
}
