using System;
using System.IO;
using System.Text.Json;

namespace AuTaskBar.Services
{
    public class SettingsService : ISettingsService
    {
        private const int CurrentSchemaVersion = 1;
        private readonly string _path;

        public SettingsService()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var app = Path.Combine(dir, "AuTaskBar");
            if (!Directory.Exists(app)) Directory.CreateDirectory(app);
            _path = Path.Combine(app, "settings.json");
        }

        public SettingsService(string settingsPath)
        {
            var folder = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _path = settingsPath;
        }

        public FocusBarSettings Load()
        {
            try
            {
                if (!File.Exists(_path)) return CreateDefault();
                var s = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<FocusBarSettings>(s) ?? CreateDefault();
                return Normalize(settings);
            }
            catch
            {
                BackupCorruptSettingsIfExists();
                return CreateDefault();
            }
        }

        public void Save(FocusBarSettings settings)
        {
            var normalized = Normalize(settings);
            var s = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });

            var temp = _path + ".tmp";
            File.WriteAllText(temp, s);

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            File.Move(temp, _path);
        }

        private static FocusBarSettings CreateDefault()
        {
            return Normalize(new FocusBarSettings());
        }

        private static FocusBarSettings Normalize(FocusBarSettings settings)
        {
            settings.SchemaVersion = CurrentSchemaVersion;
            if (settings.PomodoroFocusMinutes <= 0) settings.PomodoroFocusMinutes = 25;
            if (settings.PomodoroRestMinutes <= 0) settings.PomodoroRestMinutes = 5;
            if (settings.BarWidth < 700) settings.BarWidth = 1280;
            if (settings.BarHeight < 40) settings.BarHeight = 90;
            settings.BarOpacity = Math.Clamp(Math.Round(settings.BarOpacity, 2), 0.2, 1.0);

            if (string.IsNullOrWhiteSpace(settings.MenuPalette)) settings.MenuPalette = "Dark";
            if (string.IsNullOrWhiteSpace(settings.AppThemeMode)) settings.AppThemeMode = "Dark";
            if (string.IsNullOrWhiteSpace(settings.AppTheme)) settings.AppTheme = "Ocean";

            return settings;
        }

        private void BackupCorruptSettingsIfExists()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }

                var backup = _path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(_path, backup, overwrite: false);
            }
            catch
            {
                // Ignore backup failures.
            }
        }
    }
}
