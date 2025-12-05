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

            // Load Configuration
            ConfigManager.Load();

            // Generate Test Data
            string storageDir = ConfigManager.StorageDirectory;
            string? dbRoot = Path.GetDirectoryName(storageDir);
            if (!string.IsNullOrEmpty(dbRoot)) {
                ApiEmulator.GenerateTestFiles(dbRoot);
            }

            // Apply Language
            ApplyLanguage(ConfigManager.Current.Language);

            // Delete any temp files at startup
            /*
            try {
                string storageDir = Path.Combine(AppContext.BaseDirectory, "Database", "Storage");
                Debug.WriteLine($"App: Cleaning up temporary files at startup from {storageDir}");

                // Direct method to clean up temp files
                var tempFiles = Directory.GetFiles(storageDir, "*.temp.json");
                foreach (var file in tempFiles) {
                    try {
                        Debug.WriteLine($"App: Deleting temporary file at startup: {Path.GetFileName(file)}");
                        File.Delete(file);
                        Debug.WriteLine($"App: Successfully deleted: {Path.GetFileName(file)}");
                    } catch (Exception fileEx) {
                        Debug.WriteLine($"App: Failed to delete file {Path.GetFileName(file)}: {fileEx.Message}");
                    }
                }

                // Also use the StorageLoader method as backup
                StorageLoader.CleanupTemporaryFiles(storageDir);
            } catch (Exception ex) {
                Debug.WriteLine($"App: Error cleaning up temporary files at startup: {ex.Message}");
            }
            */
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
                // Fallback to English if failed
                if (languageCode != "en-US") {
                    ApplyLanguage("en-US");
                }
            }
        }

        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);

            /*
            try {
                // On normal exit, also clean up temp files
                string storageDir = Path.Combine(AppContext.BaseDirectory, "Database", "Storage");
                Debug.WriteLine("App: Cleaning up temporary files on exit");
                StorageLoader.CleanupTemporaryFiles(storageDir);
            } catch (Exception ex) {
                Debug.WriteLine($"App: Error cleaning up temporary files on exit: {ex.Message}");
            }
            */
        }
    }
}
