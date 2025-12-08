using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace StorageHandler.Config {
    public static class ConfigManager {
        //Initialization of dynamic variables
        private static readonly string ConfigFileName = "userconfig.json";
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        
        private static readonly string StateFileName = "appstate.json";
        private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, StateFileName);

        public static UserConfig Current { get; private set; } = new UserConfig();
        public static AppState State { get; private set; } = new AppState();

        public static string StorageDirectory {
            get {
                var path = Current.StorageFolderPath;
                if (string.IsNullOrWhiteSpace(path)) {
                    path = AppConfig.DefaultStoragePath;
                }
                
                if (Path.IsPathRooted(path)) {
                    return path;
                } else {
                    return Path.Combine(AppContext.BaseDirectory, path);
                }
            }
        }

        public static void LoadConfig() {
            if (File.Exists(ConfigPath)) {
                try {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<UserConfig>(json);
                    if (config != null) {
                        Current = config;
                        Debug.WriteLine($"ConfigManager: Loaded config. Language: {Current.Language}, Theme: {Current.Theme}");
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"ConfigManager: Failed to load config: {ex.Message}");
                }
            } else {
                Save(); // Save default config
            }
        }

        public static void LoadState() {
            if (File.Exists(StatePath)) {
                try {
                    var json = File.ReadAllText(StatePath);
                    var state = JsonSerializer.Deserialize<AppState>(json);
                    if (state != null) {
                        State = state;
                        Debug.WriteLine("ConfigManager: Loaded app state.");
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"ConfigManager: Failed to load app state: {ex.Message}");
                }
            } else {
                SaveState(); // Save default state
            }
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

        public static void SaveState() {
            try {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(State, options);
                File.WriteAllText(StatePath, json);
                Debug.WriteLine("ConfigManager: Saved app state.");
            } catch (Exception ex) {
                Debug.WriteLine($"ConfigManager: Failed to save app state: {ex.Message}");
            }
        }
    }
}
