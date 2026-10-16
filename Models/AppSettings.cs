using System.Text.Json.Serialization;

namespace BatteryReminder.Models
{
    public enum TrayIconStyle { Battery, Number }

    /// <summary>User preferences, edited from the tray menu. Stored at %AppData%\BatteryReminder\settings.json</summary>
    public sealed class AppSettings
    {
        // Alert levels
        public int CriticalBatteryThreshold { get; set; } = 10;
        public int LowBatteryThreshold { get; set; } = 25;
        public int HighBatteryThreshold { get; set; } = 90;

        public bool CriticalAlertEnabled { get; set; } = true;
        public bool LowBatteryAlertEnabled { get; set; } = true;
        public bool HighBatteryAlertEnabled { get; set; } = true;

        /// <summary>Show the alert again every N minutes while the condition persists (0 = once).</summary>
        public int RepeatReminderMinutes { get; set; } = 0;

        /// <summary>How long the on-screen banner stays (0 = until dismissed or the problem is fixed).</summary>
        public int PopupSeconds { get; set; } = 0;

        // Sound
        public bool SoundEnabled { get; set; } = true;
        public bool QuietHoursEnabled { get; set; } = false;
        public int QuietStartHour { get; set; } = 23;
        public int QuietEndHour { get; set; } = 7;

        public TrayIconStyle TrayStyle { get; set; } = TrayIconStyle.Battery;

        /// <summary>Safety-net poll in minutes. Windows power events do the real work.</summary>
        public int FallbackPollMinutes { get; set; } = 3;

        [JsonIgnore]
        public bool IsQuietNow
        {
            get
            {
                if (!QuietHoursEnabled || QuietStartHour == QuietEndHour) return false;
                int h = DateTime.Now.Hour;
                return QuietStartHour < QuietEndHour
                    ? h >= QuietStartHour && h < QuietEndHour
                    : h >= QuietStartHour || h < QuietEndHour; // wraps midnight
            }
        }

        /// <summary>Clamps values into sane ranges; false if the levels contradict each other.</summary>
        public bool Validate(out string error)
        {
            CriticalBatteryThreshold = Math.Clamp(CriticalBatteryThreshold, 1, 50);
            LowBatteryThreshold = Math.Clamp(LowBatteryThreshold, 2, 98);
            HighBatteryThreshold = Math.Clamp(HighBatteryThreshold, 3, 100);
            RepeatReminderMinutes = Math.Clamp(RepeatReminderMinutes, 0, 120);
            PopupSeconds = Math.Clamp(PopupSeconds, 0, 600);
            FallbackPollMinutes = Math.Clamp(FallbackPollMinutes, 1, 15);
            QuietStartHour = Math.Clamp(QuietStartHour, 0, 23);
            QuietEndHour = Math.Clamp(QuietEndHour, 0, 23);

            if (CriticalBatteryThreshold >= LowBatteryThreshold)
            {
                error = "Critical level must be lower than the low-battery level.";
                return false;
            }
            if (LowBatteryThreshold >= HighBatteryThreshold)
            {
                error = "Stop-charging level must be higher than the low-battery level.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
