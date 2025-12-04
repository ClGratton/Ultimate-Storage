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
            string dbRoot = Path.Combine(AppContext.BaseDirectory, "Database");
            ApiEmulator.GenerateTestFiles(dbRoot);

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
            string dictPath = "pack://application:,,,/Resources/Strings.xaml"; // Default English
            
            if (languageCode.Equals("it-IT", StringComparison.OrdinalIgnoreCase)) {
                dictPath = "pack://application:,,,/Resources/Strings.it.xaml";
            }

            try {
                var dict = new ResourceDictionary { Source = new Uri(dictPath, UriKind.Absolute) };
                
                // Find and replace the existing Strings dictionary
                // We assume it's one of the merged dictionaries
                ResourceDictionary? oldDict = null;
                foreach (var d in Resources.MergedDictionaries) {
                    if (d.Source != null && d.Source.OriginalString.Contains("Strings")) {
                        oldDict = d;
                        break;
                    }
                }

                if (oldDict != null) {
                    Resources.MergedDictionaries.Remove(oldDict);
                }
                
                Resources.MergedDictionaries.Add(dict);
                Debug.WriteLine($"App: Applied language dictionary: {dictPath}");
            } catch (Exception ex) {
                Debug.WriteLine($"App: Failed to apply language {languageCode}: {ex.Message}");
                MessageBox.Show($"Failed to load language: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
