namespace BatteryReminder.Services
{
    /// <summary>
    /// Hidden message-only window that subscribes to Windows power-setting notifications
    /// (battery %, AC/DC source, suspend/resume). This makes the app event-driven:
    /// no polling thread, zero CPU between battery changes.
    /// </summary>
    public sealed class PowerNotificationWindow : NativeWindow, IDisposable
    {
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMPOWERSTATUSCHANGE = 0x000A;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;

        private static readonly IntPtr HwndMessage = new(-3);
        private static readonly Guid GuidBatteryPercentage = new("a7ad8041-b45a-4cae-87a3-eecbb468a9e1");
        private static readonly Guid GuidAcDcSource = new("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548");

        private IntPtr _batteryReg;
        private IntPtr _sourceReg;

        /// <summary>Raised on the UI thread whenever Windows reports a power change.</summary>
        public event Action? PowerChanged;

        public PowerNotificationWindow()
        {
            CreateHandle(new CreateParams { Parent = HwndMessage });

            var g1 = GuidBatteryPercentage;
            _batteryReg = NativeMethods.RegisterPowerSettingNotification(Handle, ref g1, 0);
            var g2 = GuidAcDcSource;
            _sourceReg = NativeMethods.RegisterPowerSettingNotification(Handle, ref g2, 0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                long ev = m.WParam.ToInt64();
                if (ev == PBT_POWERSETTINGCHANGE || ev == PBT_APMPOWERSTATUSCHANGE ||
                    ev == PBT_APMRESUMESUSPEND || ev == PBT_APMRESUMEAUTOMATIC)
                {
                    PowerChanged?.Invoke();
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_batteryReg != IntPtr.Zero) { NativeMethods.UnregisterPowerSettingNotification(_batteryReg); _batteryReg = IntPtr.Zero; }
            if (_sourceReg != IntPtr.Zero) { NativeMethods.UnregisterPowerSettingNotification(_sourceReg); _sourceReg = IntPtr.Zero; }
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
