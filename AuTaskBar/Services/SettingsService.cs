using System;
using System.IO;
using System.Text.Json;

namespace AuTaskBar.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _path;
        public SettingsService()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var app = Path.Combine(dir, "AuTaskBar");
            if (!Directory.Exists(app)) Directory.CreateDirectory(app);
            _path = Path.Combine(app, "settings.json");
        }

        public FocusBarSettings Load()
        {
            try
            {
                if (!File.Exists(_path)) return new FocusBarSettings();
                var s = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<FocusBarSettings>(s) ?? new FocusBarSettings();
            }
            catch
            {
                return new FocusBarSettings();
            }
        }

        public void Save(FocusBarSettings settings)
        {
            var s = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, s);
        }
    }
}
