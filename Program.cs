using System;
using System.Threading;
using System.Windows.Forms;

namespace RestTimer
{
    internal static class Program
    {
        internal const string InstanceMutexName = @"Local\RestTimer.Singleton";
        internal const string ShutdownEventName = @"Local\RestTimer.ShutdownForInstall";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--close-for-install")
                return InstallationShutdown.CloseRunning(args[1]);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var instance = new Mutex(false, InstanceMutexName, out bool firstInstance))
            {
                if (!firstInstance)
                    return 0;

                using (var context = new RestTimerContext())
                    Application.Run(context);
            }
            return 0;
        }
    }
}
