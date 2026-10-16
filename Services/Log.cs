using System.Text;

namespace BatteryReminder.Services
{
    /// <summary>Tiny error log (%AppData%\BatteryReminder\error.log), capped at 100 KB.</summary>
    internal static class Log
    {
        private static readonly string Path = System.IO.Path.Combine(SettingsManager.AppDataFolder, "error.log");

        public static void Error(string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(SettingsManager.AppDataFolder);
                var fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > 100_000) fi.Delete();
                File.AppendAllText(Path, $"[{DateTime.Now:s}] {context}: {ex}\n", Encoding.UTF8);
            }
            catch { }
        }
    }
}
