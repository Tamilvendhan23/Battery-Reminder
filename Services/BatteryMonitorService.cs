using BatteryReminder.Models;

namespace BatteryReminder.Services
{
    public enum AlertKind { Low, High, Critical, Info }

    /// <summary>
    /// Reads the battery and evaluates alert rules (with hysteresis and optional repeat).
    /// Driven by Windows power events; the timer is only a slow safety net that speeds up near an alert level.
    /// Everything runs on the UI thread.
    /// </summary>
    public sealed class BatteryMonitorService : IDisposable
    {
        private sealed class AlertState { public bool Fired; public DateTime LastFired; }

        private readonly AppSettings _settings;
        private readonly System.Windows.Forms.Timer _poll = new();
        private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 400 };
        private readonly AlertState _low = new(), _high = new(), _critical = new();
        private bool _announced;
        private bool _disposed;

        public BatteryInfo Current { get; private set; } = BatteryInfo.Unknown;

        public event Action<BatteryInfo>? StatusChanged;
        public event Action<AlertKind, int>? AlertRaised;

        public BatteryMonitorService(AppSettings settings)
        {
            _settings = settings;
            _poll.Tick += (_, _) => Refresh();
            _debounce.Tick += (_, _) => Refresh();
        }

        public void Start() => Refresh(force: true);

        /// <summary>Coalesces bursts of power events into one read.</summary>
        public void RequestRefresh()
        {
            if (_disposed) return;
            _debounce.Stop();
            _debounce.Start();
        }

        public void Refresh(bool force = false)
        {
            if (_disposed) return;
            _debounce.Stop();

            BatteryInfo info = Read();
            bool changed = !info.SameAs(Current);
            Current = info;

            if (info.HasReading) Evaluate(info);
            ScheduleNextPoll(info);

            if (changed || force || !_announced)
            {
                _announced = true;
                StatusChanged?.Invoke(info);
            }
        }

        private static BatteryInfo Read()
        {
            try
            {
                PowerStatus p = SystemInformation.PowerStatus;
                BatteryChargeStatus cs = p.BatteryChargeStatus;
                bool csKnown = cs != BatteryChargeStatus.Unknown; // "Unknown" has every flag set
                bool plugged = p.PowerLineStatus == PowerLineStatus.Online;

                if (csKnown && cs.HasFlag(BatteryChargeStatus.NoSystemBattery))
                    return new BatteryInfo(-1, plugged, ChargeState.NoBattery, -1, DateTime.Now);

                float life = p.BatteryLifePercent;
                int pct = life >= 0f && life <= 1f ? (int)Math.Round(life * 100f) : -1;
                if (pct < 0)
                    return new BatteryInfo(-1, plugged, ChargeState.Unknown, -1, DateTime.Now);

                ChargeState state;
                if (!plugged) state = ChargeState.OnBattery;
                else if (pct >= 100) state = ChargeState.Full;
                else if (csKnown) state = cs.HasFlag(BatteryChargeStatus.Charging) ? ChargeState.Charging : ChargeState.PluggedNotCharging;
                else state = ChargeState.Charging;

                return new BatteryInfo(pct, plugged, state, plugged ? -1 : p.BatteryLifeRemaining, DateTime.Now);
            }
            catch (Exception ex)
            {
                Log.Error("Read battery", ex);
                return BatteryInfo.Unknown;
            }
        }

        // ---------------------------------------------------------------- alert rules

        private void Evaluate(BatteryInfo info)
        {
            int pct = info.Percentage;
            bool onBattery = !info.IsPluggedIn;

            // Re-arm alerts once their condition no longer holds.
            if (!onBattery) { _low.Fired = false; _critical.Fired = false; }
            else { _high.Fired = false; }
            if (pct > _settings.LowBatteryThreshold) _low.Fired = false;
            if (pct > _settings.CriticalBatteryThreshold) _critical.Fired = false;
            if (pct < _settings.HighBatteryThreshold) _high.Fired = false;

            // Critical outranks low: starting below both shows only the critical alert.
            if (onBattery && pct <= _settings.CriticalBatteryThreshold && _settings.CriticalAlertEnabled)
            {
                TryFire(_critical, AlertKind.Critical, pct);
                _low.Fired = true;
            }
            else if (onBattery && pct <= _settings.LowBatteryThreshold && _settings.LowBatteryAlertEnabled)
            {
                TryFire(_low, AlertKind.Low, pct);
            }

            if (info.IsPluggedIn && pct >= _settings.HighBatteryThreshold && _settings.HighBatteryAlertEnabled)
                TryFire(_high, AlertKind.High, pct);
        }

        private void TryFire(AlertState state, AlertKind kind, int pct)
        {
            DateTime now = DateTime.Now;
            bool repeatDue = _settings.RepeatReminderMinutes > 0 &&
                             (now - state.LastFired).TotalMinutes >= _settings.RepeatReminderMinutes;

            if (state.Fired && !repeatDue) return;

            state.Fired = true;
            state.LastFired = now;
            AlertRaised?.Invoke(kind, pct);
        }

        // ---------------------------------------------------------------- scheduling

        /// <summary>
        /// Windows power events are the primary trigger. This timer is only a safety net, so it sleeps long
        /// when the battery is far from any alert level and tightens only when an alert is close.
        /// </summary>
        private int NextPollSeconds(BatteryInfo info)
        {
            if (!info.HasReading) return 15 * 60; // no battery (desktop) or unreadable: nothing to watch

            int pct = info.Percentage;
            int distance = int.MaxValue; // percentage points to the next alert that could still fire

            if (!info.IsPluggedIn)
            {
                if (_settings.LowBatteryAlertEnabled && pct >= _settings.LowBatteryThreshold)
                    distance = Math.Min(distance, pct - _settings.LowBatteryThreshold);
                if (_settings.CriticalAlertEnabled && pct >= _settings.CriticalBatteryThreshold)
                    distance = Math.Min(distance, pct - _settings.CriticalBatteryThreshold);
            }
            else if (_settings.HighBatteryAlertEnabled && pct <= _settings.HighBatteryThreshold)
            {
                distance = _settings.HighBatteryThreshold - pct;
            }

            bool repeating = _settings.RepeatReminderMinutes > 0 && (_low.Fired || _high.Fired || _critical.Fired);
            if (repeating || distance <= 3) return 30;                        // alert is imminent
            if (distance <= 10) return _settings.FallbackPollMinutes * 60;     // getting close (default 3 min)
            return 10 * 60;                                                    // far away: events handle changes
        }

        private void ScheduleNextPoll(BatteryInfo info)
        {
            int ms = Math.Max(15, NextPollSeconds(info)) * 1000;
            if (_poll.Interval != ms || !_poll.Enabled)
            {
                _poll.Stop();
                _poll.Interval = ms;
                _poll.Start();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _poll.Dispose();
            _debounce.Dispose();
        }
    }
}
