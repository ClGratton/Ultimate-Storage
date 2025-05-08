using StorageHandler.Models;
using System;
using System.IO;
using System.Text.Json;

namespace StorageHandler.Scripts {
    public class StorageLoader {
        private readonly string _originalPath;
        private readonly string _tempPath;

        public StorageLoader(string originalPath) {
            _originalPath = originalPath;
            _tempPath = Path.ChangeExtension(originalPath, ".temp.json");
        }

        public StorageContainer? LoadStorage() {
            string pathToLoad = File.Exists(_tempPath) ? _tempPath : _originalPath;

            if (!File.Exists(pathToLoad)) {
                Console.WriteLine("Storage file not found.");
                return null;
            }

            try {
                var storageData = File.ReadAllText(pathToLoad);
                var rootContainer = JsonSerializer.Deserialize<StorageContainer>(storageData);

                if (rootContainer == null) {
                    Console.WriteLine("Failed to deserialize the root container.");
                }

                return rootContainer;
            } catch (JsonException ex) {
                Console.WriteLine($"JSON deserialization error: {ex.Message}");
                return null;
            } catch (Exception ex) {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return null;
            }
        }

        public void SaveTemporary(StorageContainer rootContainer) {
            try {
                Console.WriteLine("SaveTemporary: Saving temporary file."); // Debug statement
                var jsonData = JsonSerializer.Serialize(rootContainer, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tempPath, jsonData);
                Console.WriteLine($"Temporary file saved at: {_tempPath}"); // Debug statement
            } catch (Exception ex) {
                Console.WriteLine($"Failed to save temporary file: {ex.Message}");
            }
        }








        public void SavePermanent(StorageContainer rootContainer) {
            try {
                var jsonData = JsonSerializer.Serialize(rootContainer, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_originalPath, jsonData);
                if (File.Exists(_tempPath)) {
                    File.Delete(_tempPath); // Remove the temporary file
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed to save permanent file: {ex.Message}");
            }
        }

        public void RevertChanges() {
            if (File.Exists(_tempPath)) {
                File.Delete(_tempPath); // Delete the temporary file
            }
        }
    }
}



