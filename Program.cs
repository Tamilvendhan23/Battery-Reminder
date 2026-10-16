using BatteryReminder.Services;

namespace BatteryReminder
{
    internal static class Program
    {
        private const string AppId = "BatteryReminder_SingleInstance_v2";

        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(true, AppId, out bool isFirstInstance);
            using var activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppId + "_Activate");

            if (!isFirstInstance)
            {
                // Already running: ask the running copy to flash its status banner, then quit quietly.
                activateSignal.Set();
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            Application.ThreadException += (_, e) => Log.Error("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) Log.Error("Unhandled", ex);
            };

            NativeMethods.EnableEcoMode();

            var settings = SettingsManager.Load();

            using var app = new TrayApp(settings);

            // Second launches arrive here. A thread-pool wait costs nothing while idle (no dedicated thread).
            var sync = SynchronizationContext.Current!;
            var registration = ThreadPool.RegisterWaitForSingleObject(
                activateSignal, (_, _) => sync.Post(_ => app.ShowStatus(), null), null, Timeout.Infinite, false);

            // Trim the startup working set once things have settled.
            var trim = new System.Windows.Forms.Timer { Interval = 5000 };
            trim.Tick += (_, _) => { trim.Stop(); trim.Dispose(); NativeMethods.TrimMemory(); };
            trim.Start();

            Application.Run(app);
            registration.Unregister(null);
        }
    }
}
