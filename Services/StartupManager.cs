using Microsoft.Win32;

namespace BatteryReminder.Services
{
    /// <summary>"Start with Windows" via the per-user Run key (no admin rights). The registry is the source of truth.</summary>
    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "BatteryReminder";

        private static string Command => $"\"{Environment.ProcessPath}\"";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(ValueName) is string v && !string.IsNullOrWhiteSpace(v);
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;

                if (enabled)
                {
                    if (!string.Equals(key.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, Command, RegistryValueKind.String);
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Log.Error("Startup entry", ex); }
        }

        /// <summary>If already enabled, rewrites the entry (fixes old --minimized args or a moved exe).</summary>
        public static void Normalize()
        {
            if (IsEnabled()) SetEnabled(true);
        }
    }
}
