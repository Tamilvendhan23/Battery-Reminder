using BatteryReminder.Models;
using BatteryReminder.Services;
using BatteryReminder.UI;

namespace BatteryReminder
{
    /// <summary>
    /// The whole app: a tray icon with a settings menu, a power-event listener and the alert banner.
    /// There is no main window; everything is configured from the tray icon's right-click menu.
    /// </summary>
    public sealed class TrayApp : ApplicationContext
    {
        private readonly AppSettings _s;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu = new();
        private readonly BatteryMonitorService _svc;
        private readonly AlertPresenter _alerts;
        private readonly PowerNotificationWindow _powerWindow;

        private Icon? _icon;
        private ToolStripMenuItem _miBattery = null!;

        // Items whose check mark / enabled state / title are recomputed each time the menu opens.
        private readonly List<(ToolStripMenuItem Item, Func<bool>? Checked, Func<bool>? Enabled)> _dynamic = new();
        private readonly List<(ToolStripMenuItem Item, Func<string> Title)> _titles = new();

        public TrayApp(AppSettings settings)
        {
            _s = settings;

            _tray = new NotifyIcon { ContextMenuStrip = _menu };
            _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowStatus(); };

            _alerts = new AlertPresenter(_s, _tray);
            _svc = new BatteryMonitorService(_s);
            _svc.StatusChanged += OnStatusChanged;
            _svc.AlertRaised += (kind, pct) => _alerts.ShowAlert(kind, pct);

            BuildMenu();

            _powerWindow = new PowerNotificationWindow();
            _powerWindow.PowerChanged += _svc.RequestRefresh;

            StartupManager.Normalize();
            _tray.Visible = true;
            _svc.Start();
        }

        /// <summary>Brief status banner; also used when the app is launched a second time.</summary>
        public void ShowStatus() => _alerts.ShowStatus(_svc.Current);

        // ------------------------------------------------------------------ status -> tray

        private void OnStatusChanged(BatteryInfo info)
        {
            Icon fresh = IconHelper.Create(info, _s);
            _tray.Icon = fresh;
            _icon?.Dispose();
            _icon = fresh;

            string tip = info.HasReading ? $"Battery {info.Percentage}% · {info.StatusText}" : info.StatusText;
            if (info.RemainingText.Length > 0) tip += " · " + info.RemainingText;
            _tray.Text = tip.Length > 120 ? tip[..120] : tip;

            _alerts.OnStatus(info);
        }

        private void ApplySettingsChange()
        {
            if (!_s.Validate(out _)) return;
            SettingsManager.Save(_s);
            _svc.Refresh(force: true); // re-evaluates alerts and redraws the tray icon
        }

        // ------------------------------------------------------------------ menu

        private ToolStripMenuItem Submenu(string text, Func<string>? title = null)
        {
            var item = new ToolStripMenuItem(text);
            _menu.Items.Add(item);
            if (title != null) _titles.Add((item, title));
            return item;
        }

        private void Choice(ToolStripItemCollection target, string text, Func<bool> isChecked, Action apply, Func<bool>? enabled = null)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => { apply(); ApplySettingsChange(); };
            target.Add(item);
            _dynamic.Add((item, isChecked, enabled));
        }

        private void BuildMenu()
        {
            _miBattery = new ToolStripMenuItem("Battery") { Enabled = false };
            _menu.Items.Add(_miBattery);
            _menu.Items.Add(new ToolStripSeparator());

            // ---- Alert levels (presets that would contradict another level are greyed out)
            var crit = Submenu("Critical alert", () => _s.CriticalAlertEnabled ? $"Critical alert: {_s.CriticalBatteryThreshold}%" : "Critical alert: off");
            Choice(crit.DropDownItems, "Off", () => !_s.CriticalAlertEnabled, () => _s.CriticalAlertEnabled = false);
            foreach (int v in new[] { 5, 8, 10, 15, 20 })
                Choice(crit.DropDownItems, $"{v}%",
                    () => _s.CriticalAlertEnabled && _s.CriticalBatteryThreshold == v,
                    () => { _s.CriticalBatteryThreshold = v; _s.CriticalAlertEnabled = true; },
                    () => v < _s.LowBatteryThreshold);

            var low = Submenu("Low battery alert", () => _s.LowBatteryAlertEnabled ? $"Low battery alert: {_s.LowBatteryThreshold}%" : "Low battery alert: off");
            Choice(low.DropDownItems, "Off", () => !_s.LowBatteryAlertEnabled, () => _s.LowBatteryAlertEnabled = false);
            foreach (int v in new[] { 15, 20, 25, 30, 35, 40, 50 })
                Choice(low.DropDownItems, $"{v}%",
                    () => _s.LowBatteryAlertEnabled && _s.LowBatteryThreshold == v,
                    () => { _s.LowBatteryThreshold = v; _s.LowBatteryAlertEnabled = true; },
                    () => v > _s.CriticalBatteryThreshold && v < _s.HighBatteryThreshold);

            var high = Submenu("Stop-charging alert", () => _s.HighBatteryAlertEnabled ? $"Stop-charging alert: {_s.HighBatteryThreshold}%" : "Stop-charging alert: off");
            Choice(high.DropDownItems, "Off", () => !_s.HighBatteryAlertEnabled, () => _s.HighBatteryAlertEnabled = false);
            foreach (int v in new[] { 80, 85, 90, 95, 100 })
                Choice(high.DropDownItems, $"{v}%",
                    () => _s.HighBatteryAlertEnabled && _s.HighBatteryThreshold == v,
                    () => { _s.HighBatteryThreshold = v; _s.HighBatteryAlertEnabled = true; },
                    () => v > _s.LowBatteryThreshold);

            _menu.Items.Add(new ToolStripSeparator());

            // ---- Reminder behaviour
            var repeat = Submenu("Repeat reminder", () => _s.RepeatReminderMinutes == 0 ? "Repeat reminder: off" : $"Repeat reminder: every {_s.RepeatReminderMinutes} min");
            Choice(repeat.DropDownItems, "Off (remind once)", () => _s.RepeatReminderMinutes == 0, () => _s.RepeatReminderMinutes = 0);
            foreach (int m in new[] { 1, 2, 5, 10, 15, 30 })
                Choice(repeat.DropDownItems, $"Every {m} min", () => _s.RepeatReminderMinutes == m, () => _s.RepeatReminderMinutes = m);

            var banner = Submenu("Banner stays", () => _s.PopupSeconds == 0 ? "Banner stays: until dismissed" : $"Banner stays: {_s.PopupSeconds} s");
            Choice(banner.DropDownItems, "Until dismissed or fixed", () => _s.PopupSeconds == 0, () => _s.PopupSeconds = 0);
            foreach (int sec in new[] { 10, 30, 60 })
                Choice(banner.DropDownItems, $"{sec} seconds", () => _s.PopupSeconds == sec, () => _s.PopupSeconds = sec);

            var sound = Submenu("Sound");
            Choice(sound.DropDownItems, "Play alert sound", () => _s.SoundEnabled, () => _s.SoundEnabled = !_s.SoundEnabled);
            sound.DropDownItems.Add(new ToolStripSeparator());
            Choice(sound.DropDownItems, "No quiet hours", () => !_s.QuietHoursEnabled, () => _s.QuietHoursEnabled = false);
            foreach (var (startHour, endHour) in new[] { (22, 7), (23, 7), (0, 6) })
                Choice(sound.DropDownItems, $"Mute {startHour:00}:00 – {endHour:00}:00",
                    () => _s.QuietHoursEnabled && _s.QuietStartHour == startHour && _s.QuietEndHour == endHour,
                    () => { _s.QuietHoursEnabled = true; _s.QuietStartHour = startHour; _s.QuietEndHour = endHour; });

            var style = Submenu("Tray icon");
            Choice(style.DropDownItems, "Battery", () => _s.TrayStyle == TrayIconStyle.Battery, () => _s.TrayStyle = TrayIconStyle.Battery);
            Choice(style.DropDownItems, "Percentage number", () => _s.TrayStyle == TrayIconStyle.Number, () => _s.TrayStyle = TrayIconStyle.Number);

            _menu.Items.Add(new ToolStripSeparator());

            var test = new ToolStripMenuItem("Send test alert");
            test.Click += (_, _) => _alerts.ShowAlert(AlertKind.Low, _svc.Current.HasReading ? _svc.Current.Percentage : _s.LowBatteryThreshold, isTest: true);
            _menu.Items.Add(test);

            Choice(_menu.Items, "Start with Windows", StartupManager.IsEnabled, () => StartupManager.SetEnabled(!StartupManager.IsEnabled()));

            _menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("Exit");
            exit.Click += (_, _) => ExitThread();
            _menu.Items.Add(exit);

            // Check marks and labels are refreshed only when the menu opens, never on battery changes.
            _menu.Opening += (_, _) => RefreshMenu();
        }

        private void RefreshMenu()
        {
            BatteryInfo b = _svc.Current;
            string line = b.HasReading ? $"{b.Percentage}% · {b.StatusText}" : b.StatusText;
            if (b.RemainingText.Length > 0) line += " · " + b.RemainingText;
            _miBattery.Text = line;

            foreach (var (item, isChecked, enabled) in _dynamic)
            {
                if (isChecked != null) item.Checked = isChecked();
                if (enabled != null) item.Enabled = enabled();
            }
            foreach (var (item, title) in _titles) item.Text = title();
        }

        // ------------------------------------------------------------------ shutdown

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _alerts.Dispose();
                _powerWindow.Dispose();
                _svc.Dispose();
                _tray.Visible = false;
                _tray.Dispose();
                _menu.Dispose();
                _icon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
