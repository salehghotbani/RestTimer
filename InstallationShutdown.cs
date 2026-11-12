using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace RestTimer
{
    internal static class InstallationShutdown
    {
        private const uint QueryLimitedInformation = 0x1000;
        private const uint QueryToken = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // Exit 10 tells Setup that an existing instance was closed. The UTF-16
        // INI lets Setup restore its original path if installation is cancelled.
        public static int CloseRunning(string stateFile)
        {
            bool closed = false;
            using (var current = Process.GetCurrentProcess())
            using (var identity = WindowsIdentity.GetCurrent())
            {
                foreach (var process in Process.GetProcessesByName("RestTimer"))
                {
                    using (process)
                    {
                        try
                        {
                            if (process.Id == current.Id || process.SessionId != current.SessionId)
                                continue;
                            string path = GetOwnExecutable(process.Id, identity.User);
                            if (path == null)
                                continue;
                            if (!closed)
                                File.WriteAllText(stateFile, "[State]\r\nExecutable=" + path + "\r\n", Encoding.Unicode);
                            if (!CloseInstance(process))
                                return 1;
                            closed = true;
                        }
                        catch (InvalidOperationException)
                        {
                            // An instance may exit between discovery and opening it.
                        }
                        catch (Exception ex) when (ex is Win32Exception || ex is IOException
                            || ex is UnauthorizedAccessException || ex is SecurityException)
                        {
                            return 1;
                        }
                    }
                }
            }
            return closed ? 10 : 0;
        }

        private static string GetOwnExecutable(int processId, SecurityIdentifier user)
        {
            IntPtr handle = OpenProcess(QueryLimitedInformation, false, processId);
            if (handle == IntPtr.Zero)
                return null;
            try
            {
                if (!OpenProcessToken(handle, QueryToken, out IntPtr token))
                    return null;
                try
                {
                    using (var identity = new WindowsIdentity(token))
                        if (identity.User != user)
                            return null;
                }
                finally
                {
                    CloseHandle(token);
                }

                var path = new StringBuilder(32768);
                int size = path.Capacity;
                if (!QueryFullProcessImageName(handle, 0, path, ref size))
                    return null;
                var version = FileVersionInfo.GetVersionInfo(path.ToString());
                if (version.ProductName != "RestTimer"
                    || !string.Equals(version.OriginalFilename, "RestTimer.exe", StringComparison.OrdinalIgnoreCase))
                    return null;
                return path.ToString();
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static bool CloseInstance(Process process)
        {
            if (process.HasExited)
                return true;

            if (EventWaitHandle.TryOpenExisting(Program.ShutdownEventName,
                EventWaitHandleRights.Modify, out EventWaitHandle request))
            {
                using (request)
                    request.Set();
                if (process.WaitForExit(4000))
                    return true;
            }

            // Older releases have no shutdown event and may have only a tray icon.
            if (process.CloseMainWindow() && process.WaitForExit(2000))
                return true;
            if (!process.HasExited)
                process.Kill();
            return process.WaitForExit(5000);
        }
    }
}
