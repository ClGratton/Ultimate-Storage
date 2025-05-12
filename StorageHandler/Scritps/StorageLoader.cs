using StorageHandler.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace StorageHandler.Scripts {
    public class StorageLoader {
        private readonly string _originalPath;
        private readonly string _tempPath;

        public StorageLoader(string originalPath) {
            _originalPath = originalPath;
            _tempPath = Path.ChangeExtension(originalPath, ".temp.json");
            Debug.WriteLine($"StorageLoader: Initialized with path: {originalPath}");
            Debug.WriteLine($"StorageLoader: Temp path: {_tempPath}");
        }

        public StorageContainer? LoadStorage() {
            string pathToLoad = File.Exists(_tempPath) ? _tempPath : _originalPath;
            Debug.WriteLine($"StorageLoader: Loading from {pathToLoad}");

            if (!File.Exists(pathToLoad)) {
                Debug.WriteLine("StorageLoader: File not found");
                MessageBox.Show("Storage file not found. Creating a new empty storage.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return CreateDefaultContainer();
            }

            try {
                var storageData = File.ReadAllText(pathToLoad);
                Debug.WriteLine($"StorageLoader: Loaded {storageData.Length} bytes from file");

                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };
                var rootContainer = JsonSerializer.Deserialize<StorageContainer>(storageData, options);
                if (rootContainer == null) {
                    Debug.WriteLine("StorageLoader: Deserialization returned null");
                    MessageBox.Show("Deserialization returned null. Using default storage.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return CreateDefaultContainer();
                }

                // Debug output to verify positions
                Debug.WriteLine($"StorageLoader: Loaded root container with {rootContainer.Children.Count} children");
                foreach (var child in rootContainer.Children) {
                    Debug.WriteLine($"StorageLoader: Child {child.Name} at position [{child.Position[0]}, {child.Position[1]}] size [{child.Size[0]}, {child.Size[1]}]");
                }

                return rootContainer;
            } catch (JsonException ex) {
                Debug.WriteLine($"StorageLoader: JSON format error: {ex.Message}");
                MessageBox.Show($"JSON format error: {ex.Message}\nUsing default storage instead.", "JSON Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return CreateDefaultContainer();
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Unexpected error: {ex.Message}");
                MessageBox.Show($"Unexpected error: {ex.Message}\nUsing default storage instead.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return CreateDefaultContainer();
            }
        }

        private StorageContainer CreateDefaultContainer() {
            Debug.WriteLine("StorageLoader: Creating default container");
            return new StorageContainer {
                Name = "Default",
                Type = "container",
                Allowed = new List<string> { "electronics" },
                Color = "#F8F9FA",
                Position = new int[2] { 0, 0 },
                Size = new int[2] { 1, 1 },
                Children = new List<StorageContainer>()
            };
        }

        public void SaveTemporary(StorageContainer rootContainer) {
            try {
                var jsonData = JsonSerializer.Serialize(rootContainer, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tempPath, jsonData);
                Debug.WriteLine($"StorageLoader: Saved temporary data to {_tempPath}");

                // Debug output to verify positions being saved
                foreach (var child in rootContainer.Children) {
                    Debug.WriteLine($"StorageLoader: Saved {child.Name} at position [{child.Position[0]}, {child.Position[1]}] size [{child.Size[0]}, {child.Size[1]}]");
                }
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save temporary file: {ex.Message}");
                Console.WriteLine($"Failed to save temporary file: {ex.Message}");
            }
        }

        public void SavePermanent(StorageContainer rootContainer) {
            try {
                var jsonData = JsonSerializer.Serialize(rootContainer, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_originalPath, jsonData);
                Debug.WriteLine($"StorageLoader: Saved permanent data to {_originalPath}");

                if (File.Exists(_tempPath)) {
                    File.Delete(_tempPath);
                    Debug.WriteLine("StorageLoader: Deleted temporary file");
                }
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save permanent file: {ex.Message}");
                Console.WriteLine($"Failed to save permanent file: {ex.Message}");
            }
        }

        public void RevertChanges() {
            if (File.Exists(_tempPath)) {
                File.Delete(_tempPath);
                Debug.WriteLine("StorageLoader: Deleted temporary file (reverting changes)");
            }
        }

        public static void CleanupTemporaryFiles(string directoryPath) {
            Debug.WriteLine($"StorageLoader: Starting cleanup of temporary files in {directoryPath}");
            try {
                // Check if directory exists
                if (!Directory.Exists(directoryPath)) {
                    Debug.WriteLine($"StorageLoader: Directory not found: {directoryPath}");
                    return;
                }

                // Find all temporary JSON files in the directory
                var tempFiles = Directory.GetFiles(directoryPath, "*.temp.json");
                Debug.WriteLine($"StorageLoader: Found {tempFiles.Length} temporary files");

                foreach (var file in tempFiles) {
                    try {
                        Debug.WriteLine($"StorageLoader: Deleting temporary file: {Path.GetFileName(file)}");
                        File.Delete(file);
                        Debug.WriteLine($"StorageLoader: Successfully deleted: {Path.GetFileName(file)}");
                    } catch (Exception fileEx) {
                        Debug.WriteLine($"StorageLoader: Failed to delete file {Path.GetFileName(file)}: {fileEx.Message}");
                    }
                }
                Debug.WriteLine("StorageLoader: Temporary file cleanup complete");
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Error during temporary file cleanup: {ex.Message}");
            }
        }


    }
}
