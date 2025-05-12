using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using StorageHandler.Scripts;

namespace StorageHandler {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            // Delete any temp files at startup
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
        }

        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);

            try {
                // On normal exit, also clean up temp files
                string storageDir = Path.Combine(AppContext.BaseDirectory, "Database", "Storage");
                Debug.WriteLine("App: Cleaning up temporary files on exit");
                StorageLoader.CleanupTemporaryFiles(storageDir);
            } catch (Exception ex) {
                Debug.WriteLine($"App: Error cleaning up temporary files on exit: {ex.Message}");
            }
        }
    }
}
