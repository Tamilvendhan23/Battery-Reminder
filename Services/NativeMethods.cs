using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BatteryReminder.Services
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

        [DllImport("user32.dll")]
        public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref ProcessPowerThrottlingState info, int size);

        /// <summary>Windows 11 "Efficiency mode" (EcoQoS) for this process: runs on efficient cores at low clocks.</summary>
        public static void EnableEcoMode()
        {
            try
            {
                var state = new ProcessPowerThrottlingState { Version = 1, ControlMask = 1, StateMask = 1 };
                SetProcessInformation(Process.GetCurrentProcess().Handle, 4, ref state, Marshal.SizeOf<ProcessPowerThrottlingState>());
            }
            catch { /* older Windows: ignore */ }
        }

        /// <summary>Returns unused memory to Windows. Called after the window closes.</summary>
        public static void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        /// <summary>Re-asserts "always on top" without stealing focus (SWP_NOSIZE|NOMOVE|NOACTIVATE|SHOWWINDOW).</summary>
        public static void KeepOnTop(IntPtr hwnd)
        {
            try { SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040); }
            catch { }
        }
    }
}
