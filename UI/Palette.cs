using Microsoft.Win32;

namespace BatteryReminder.UI
{
    /// <summary>Colours shared by the tray icon and the alert banner.</summary>
    public static class Palette
    {
        public static readonly Color Info = Color.FromArgb(59, 130, 246);
        public static readonly Color Good = Color.FromArgb(16, 185, 129);
        public static readonly Color Warn = Color.FromArgb(245, 158, 11);
        public static readonly Color Bad = Color.FromArgb(239, 68, 68);

        public static Color Level(int percent, bool charging, int low, int critical)
        {
            if (charging) return Good;
            if (percent <= critical) return Bad;
            if (percent <= low) return Warn;
            return Info;
        }

        /// <summary>True when the taskbar is light (decides the tray icon outline colour).</summary>
        public static bool TaskbarIsLight()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("SystemUsesLightTheme") is not int v || v != 0;
            }
            catch { return false; }
        }
    }
}
