using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace StorageHandler.Config {
    public static class ConfigManager {
        private static readonly string ConfigFileName = "userconfig.json";
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        
        public static UserConfig Current { get; private set; } = new UserConfig();

        public static void Load() {
            if (File.Exists(ConfigPath)) {
                try {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<UserConfig>(json);
                    if (config != null) {
                        Current = config;
                        Debug.WriteLine($"ConfigManager: Loaded config. Language: {Current.Language}, Theme: {Current.Theme}");
                        return;
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"ConfigManager: Failed to load config: {ex.Message}");
                }
            }
            
            // If load failed or file doesn't exist, save default
            Save();
        }

        public static void Save() {
            try {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(ConfigPath, json);
                Debug.WriteLine("ConfigManager: Saved config.");
            } catch (Exception ex) {
                Debug.WriteLine($"ConfigManager: Failed to save config: {ex.Message}");
            }
        }
    }
}
