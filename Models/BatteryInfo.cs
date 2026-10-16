namespace BatteryReminder.Models
{
    public enum ChargeState { Unknown, NoBattery, OnBattery, Charging, PluggedNotCharging, Full }

    /// <summary>Immutable snapshot of the battery at one moment.</summary>
    public sealed record BatteryInfo(
        int Percentage,
        bool IsPluggedIn,
        ChargeState State,
        int RemainingSeconds,
        DateTime Timestamp)
    {
        public static BatteryInfo Unknown { get; } = new(-1, false, ChargeState.Unknown, -1, DateTime.Now);

        public bool HasReading => Percentage >= 0;
        public bool IsCharging => State == ChargeState.Charging;

        /// <summary>"2h 10m left" (Windows' estimate, only available on battery) or empty.</summary>
        public string RemainingText
        {
            get
            {
                if (RemainingSeconds <= 0) return string.Empty;
                var t = TimeSpan.FromSeconds(RemainingSeconds);
                int h = (int)t.TotalHours;
                return h > 0 ? $"{h}h {t.Minutes:00}m left" : $"{Math.Max(1, t.Minutes)} min left";
            }
        }

        public string StatusText => State switch
        {
            ChargeState.NoBattery => "No battery detected",
            ChargeState.Charging => "Charging",
            ChargeState.Full => "Fully charged",
            ChargeState.PluggedNotCharging => "Plugged in, not charging",
            ChargeState.OnBattery => "On battery",
            _ => "Status unknown"
        };

        /// <summary>Same reading? Used to skip redundant UI / icon work.</summary>
        public bool SameAs(BatteryInfo? o) =>
            o != null && o.Percentage == Percentage && o.State == State && o.IsPluggedIn == IsPluggedIn;
    }
}
