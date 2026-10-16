using System.Text.Json;
using System.Text.Json.Serialization;
using BatteryReminder.Models;

namespace BatteryReminder.Services
{
    /// <summary>Loads/saves %AppData%\BatteryReminder\settings.json (atomic writes, tolerant reads).</summary>
    public static class SettingsManager
    {
        internal static readonly string AppDataFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryReminder");

        private static readonly string FilePath = System.IO.Path.Combine(AppDataFolder, "settings.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // stays compatible with v1 files
            Converters = { new JsonStringEnumConverter() }
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
                    if (s != null)
                    {
                        if (!s.Validate(out _))
                        {
                            // Upgrade path from v1 (no critical level): derive one instead of discarding the file.
                            s.CriticalBatteryThreshold = Math.Max(1, s.LowBatteryThreshold / 2);
                            if (!s.Validate(out _)) return Defaults();
                        }
                        return s;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Load settings", ex); }

            return Defaults();
        }

        private static AppSettings Defaults()
        {
            var d = new AppSettings();
            Save(d);
            return d;
        }

        public static bool Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
                File.Move(tmp, FilePath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Save settings", ex);
                return false;
            }
        }
    }
}
