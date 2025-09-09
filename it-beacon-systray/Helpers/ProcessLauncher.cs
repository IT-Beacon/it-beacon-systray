using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace it_beacon_systray
{
    public static class ProcessLauncher
    {
        // Win32 API Imports for launching a process in the user's session
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint SessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        /// <summary>
        /// Launches the current application in the active user's session.
        /// </summary>
        public static void StartProcessInUserSession()
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF) // No active user session
            {
                Debug.WriteLine("[ProcessLauncher] No active user session found.");
                return;
            }

            if (!WTSQueryUserToken(sessionId, out IntPtr hUserToken))
            {
                Debug.WriteLine("[ProcessLauncher] Failed to get user token.");
                return;
            }

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "winsta0\\default"; // Standard desktop

            uint dwCreationFlags = 0x00000010; // NORMAL_PRIORITY_CLASS

            // Get the full path of the current executable
            string commandLine = Process.GetCurrentProcess().MainModule!.FileName!;

            CreateProcessAsUser(
                hUserToken,
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                dwCreationFlags,
                IntPtr.Zero,
                null,
                ref si,
                out _);
        }
    }
}
