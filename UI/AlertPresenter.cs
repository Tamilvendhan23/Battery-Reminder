using System.Media;
using BatteryReminder.Models;
using BatteryReminder.Services;

namespace BatteryReminder.UI
{
    /// <summary>Plays the alert sound and shows / updates / closes the on-screen banner.</summary>
    public sealed class AlertPresenter : IDisposable
    {
        private readonly AppSettings _s;
        private readonly NotifyIcon _tray;
        private AlertPopup? _popup;

        public AlertPresenter(AppSettings settings, NotifyIcon tray)
        {
            _s = settings;
            _tray = tray;
        }

        public void ShowAlert(AlertKind kind, int percent, bool isTest = false)
        {
            (string title, string text, ToolTipIcon icon) = kind switch
            {
                AlertKind.Critical => ("Battery critical", "Plug in your charger now.", ToolTipIcon.Error),
                AlertKind.Low => ("Battery low", "Plug in your charger.", ToolTipIcon.Warning),
                _ => ("Battery charged", "Unplug your charger to protect the battery.", ToolTipIcon.Info)
            };
            if (isTest) text = "Test alert. This is how reminders will appear.";

            PlaySound(kind);
            Present(kind, title, text, percent, _s.PopupSeconds, icon);
        }

        /// <summary>Brief status banner (tray left-click, or launching the app a second time).</summary>
        public void ShowStatus(BatteryInfo info)
        {
            if (_popup != null && !_popup.IsDisposed && _popup.Kind != AlertKind.Info) return; // never cover a real alert

            string text = info.StatusText;
            string left = info.RemainingText;
            if (left.Length > 0) text += " · " + left;
            Present(AlertKind.Info, "Battery status", text, info.Percentage, 4, ToolTipIcon.Info);
        }

        /// <summary>Keeps the banner current, and closes it as soon as the problem is solved (charger plugged in, etc.).</summary>
        public void OnStatus(BatteryInfo info)
        {
            if (_popup == null || _popup.IsDisposed || _popup.Kind == AlertKind.Info) return;

            bool resolved = !info.HasReading || (_popup.Kind switch
            {
                AlertKind.Critical => info.IsPluggedIn || info.Percentage > _s.CriticalBatteryThreshold,
                AlertKind.Low => info.IsPluggedIn || info.Percentage > _s.LowBatteryThreshold,
                AlertKind.High => !info.IsPluggedIn || info.Percentage < _s.HighBatteryThreshold,
                _ => false
            });

            if (resolved) _popup.Close();
            else _popup.UpdatePercent(info.Percentage);
        }

        private void Present(AlertKind kind, string title, string text, int percent, int seconds, ToolTipIcon fallbackIcon)
        {
            try
            {
                if (_popup == null || _popup.IsDisposed)
                {
                    var popup = new AlertPopup();
                    popup.FormClosed += (_, _) => { if (ReferenceEquals(_popup, popup)) _popup = null; };
                    _popup = popup;
                }
                _popup.SetContent(kind, title, text, percent, seconds);
                _popup.Present();
            }
            catch (Exception ex)
            {
                Log.Error("Banner", ex);
                try { _tray.ShowBalloonTip(8000, title, text, fallbackIcon); } catch { }
            }
        }

        private void PlaySound(AlertKind kind)
        {
            if (!_s.SoundEnabled || _s.IsQuietNow) return;
            try
            {
                switch (kind)
                {
                    case AlertKind.Critical: SystemSounds.Hand.Play(); break;
                    case AlertKind.Low: SystemSounds.Exclamation.Play(); break;
                    default: SystemSounds.Asterisk.Play(); break;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_popup != null && !_popup.IsDisposed) _popup.Close();
            _popup = null;
        }
    }
}
