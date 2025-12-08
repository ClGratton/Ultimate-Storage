using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using StorageHandler.Config;
using StorageHandler.Scripts;

namespace StorageHandler {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            ConfigManager.LoadConfig();
            ConfigManager.LoadState();

            // Generate Debug Test Data
            if (DebugConfig.GenerateTestData) {
                string storageDir = ConfigManager.StorageDirectory;
                string? dbRoot = Path.GetDirectoryName(storageDir);
                if (!string.IsNullOrEmpty(dbRoot)) {
                    ApiEmulator.GenerateTestFiles(dbRoot);
                }
            }

            ApplyLanguage(ConfigManager.Current.Language);
        }

        private void ApplyLanguage(string languageCode) {
            try {
                var culture = new System.Globalization.CultureInfo(languageCode);
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                StorageHandler.Properties.Resources.Culture = culture;
                Debug.WriteLine($"App: Applied language: {languageCode}");
            } catch (Exception ex) {
                Debug.WriteLine($"App: Failed to apply language {languageCode}: {ex.Message}");
                if (languageCode != "en-US") {
                    ApplyLanguage("en-US");
                }
            }
        }

        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);

            try {
                // On normal exit, also clean up temp files
                string storageDir = ConfigManager.StorageDirectory;
                Debug.WriteLine("App: Cleaning up temporary files on exit");
                StorageLoader.CleanupTemporaryFiles(storageDir);
            } catch (Exception ex) {
                Debug.WriteLine($"App: Error cleaning up temporary files on exit: {ex.Message}");
            }
        }
    }
}
