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
        private readonly string _itemsDirectory;
        private readonly string _componentsPath;
        private readonly string _componentsPersonalPath;
        private readonly string _modelsPath;

        public event Action<bool>? UnsavedChangesChanged;
        public bool HasUnsavedChanges => File.Exists(_tempPath);

        public StorageLoader(string originalPath) {
            _originalPath = originalPath;
            _tempPath = Path.ChangeExtension(originalPath, ".temp.json");
            
            // Setup Items directory: .../Database/Items/
            string storageDir = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string databaseDir = Path.GetDirectoryName(storageDir) ?? string.Empty;
            _itemsDirectory = Path.Combine(databaseDir, "Items");
            _componentsPath = Path.Combine(databaseDir, "components.json");
            _componentsPersonalPath = Path.Combine(databaseDir, "components_personal.json");
            _modelsPath = Path.Combine(databaseDir, "models.json");
            
            if (!Directory.Exists(_itemsDirectory)) {
                Directory.CreateDirectory(_itemsDirectory);
            }

            // Migration: If components.json exists but components_personal.json does not,
            // and components.json contains non-standard items, we should probably treat it as personal?
            // For simplicity, let's just ensure components.json is the standard one, and if personal is missing, create it empty.
            // But to avoid data loss, if personal is missing, we could copy current components.json to it?
            // Let's just implement the Load logic to merge them.

            Debug.WriteLine($"StorageLoader: Initialized with path: {originalPath}");
            Debug.WriteLine($"StorageLoader: Temp path: {_tempPath}");
            Debug.WriteLine($"StorageLoader: Items directory: {_itemsDirectory}");
            Debug.WriteLine($"StorageLoader: Components path: {_componentsPath}");
            Debug.WriteLine($"StorageLoader: Personal Components path: {_componentsPersonalPath}");
            Debug.WriteLine($"StorageLoader: Models path: {_modelsPath}");
        }

        public List<ComponentModel> LoadModels() {
            if (!File.Exists(_modelsPath)) return new List<ComponentModel>();
            try {
                var json = File.ReadAllText(_modelsPath);
                bool migrated = false;

                // Migration: If the file uses "componentName", replace it with "category"
                if (json.Contains("\"componentName\"")) {
                    Debug.WriteLine("StorageLoader: Migrating models.json from ComponentModel to StorageItem format");
                    json = json.Replace("\"componentName\"", "\"category\"");
                    migrated = true;
                }

                // Migration: If the file uses "name", replace it with "category"
                if (json.Contains("\"name\"")) {
                    Debug.WriteLine("StorageLoader: Migrating models.json from Name to Category format");
                    json = json.Replace("\"name\"", "\"category\"");
                    migrated = true;
                }

                var items = JsonSerializer.Deserialize<List<ComponentModel>>(json) ?? new List<ComponentModel>();
                
                if (migrated) {
                    SaveModels(items);
                }

                return items;
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to load models: {ex.Message}");
                return new List<ComponentModel>();
            }
        }

        public void SaveModels(List<ComponentModel> models) {
            try {
                var json = JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_modelsPath, json);
                Debug.WriteLine("StorageLoader: Saved models");
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save models: {ex.Message}");
            }
        }

        public List<ComponentDefinition> LoadComponents() {
            var components = new List<ComponentDefinition>();
            
            // 1. Load Standard Categories from File (Dynamic, not hardcoded)
            if (File.Exists(_componentsPath)) {
                try {
                    var json = File.ReadAllText(_componentsPath);
                    var standard = JsonSerializer.Deserialize<List<ComponentDefinition>>(json);
                    if (standard != null) components.AddRange(standard);
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to load standard components: {ex.Message}");
                }
            } else {
                // First run: Initialize with realistic defaults if file is missing
                var defaults = DatabaseSeeder.GetCommonCategories();
                components.AddRange(defaults);
                SaveComponents(components); // Create the file
            }

            // 2. Load Personal Categories from File
            if (File.Exists(_componentsPersonalPath)) {
                try {
                    var json = File.ReadAllText(_componentsPersonalPath);
                    var personal = JsonSerializer.Deserialize<List<ComponentDefinition>>(json);
                    if (personal != null) {
                        foreach (var p in personal) {
                            if (!components.Any(c => c.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) {
                                components.Add(p);
                            }
                        }
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to load personal components: {ex.Message}");
                }
            }

            return components;
        }

        public void SaveComponents(List<ComponentDefinition> components) {
            var standard = DatabaseSeeder.GetCommonCategories();
            var personal = new List<ComponentDefinition>();

            foreach (var c in components) {
                // If it's not in the standard list, it's personal
                if (!standard.Any(s => s.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))) {
                    personal.Add(c);
                }
            }

            try {
                var json = JsonSerializer.Serialize(personal, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_componentsPersonalPath, json);
                Debug.WriteLine("StorageLoader: Saved personal components");
                
                // Also save standard ones to components.json just for reference/backup, or leave it alone?
                // Let's save the standard ones to components.json so it looks clean
                var standardJson = JsonSerializer.Serialize(standard, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_componentsPath, standardJson);
                
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save components: {ex.Message}");
            }
        }

        public List<StorageItem> LoadItems(string containerName) {
            string filePath = Path.Combine(_itemsDirectory, $"{containerName}.json");
            if (!File.Exists(filePath)) return new List<StorageItem>();

            try {
                var json = File.ReadAllText(filePath);
                bool migrated = false;

                if (json.Contains("\"componentName\"")) {
                    json = json.Replace("\"componentName\"", "\"category\"");
                    migrated = true;
                }
                if (json.Contains("\"name\"")) {
                    json = json.Replace("\"name\"", "\"category\"");
                    migrated = true;
                }

                var items = JsonSerializer.Deserialize<List<StorageItem>>(json) ?? new List<StorageItem>();

                if (migrated) {
                    SaveItems(containerName, items);
                }

                return items;
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to load items for {containerName}: {ex.Message}");
                return new List<StorageItem>();
            }
        }

        public void SaveItems(string containerName, List<StorageItem> items) {
            try {
                string filePath = Path.Combine(_itemsDirectory, $"{containerName}.json");
                var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Debug.WriteLine($"StorageLoader: Saved items for {containerName}");
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save items for {containerName}: {ex.Message}");
            }
        }

        public void RenameItemFile(string oldName, string newName) {
            string oldPath = Path.Combine(_itemsDirectory, $"{oldName}.json");
            string newPath = Path.Combine(_itemsDirectory, $"{newName}.json");
            
            if (File.Exists(oldPath)) {
                try {
                    if (File.Exists(newPath)) File.Delete(newPath);
                    File.Move(oldPath, newPath);
                    Debug.WriteLine($"StorageLoader: Renamed item file from {oldName} to {newName}");
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to rename item file: {ex.Message}");
                }
            }
        }

        public void DeleteItems(string containerName) {
            string filePath = Path.Combine(_itemsDirectory, $"{containerName}.json");
            if (File.Exists(filePath)) {
                try {
                    File.Delete(filePath);
                    Debug.WriteLine($"StorageLoader: Deleted items file for {containerName}");
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to delete items file for {containerName}: {ex.Message}");
                }
            }
        }

        public StorageContainer? LoadStorage() {
            string pathToLoad = File.Exists(_tempPath) ? _tempPath : _originalPath;
            Debug.WriteLine($"StorageLoader: Loading from {pathToLoad}");

            if (!File.Exists(pathToLoad)) {
                Debug.WriteLine("StorageLoader: File not found");
                // Silently create default container without prompting
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
                    // Silently create default container without prompting
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
                // Silently create default container without prompting
                return CreateDefaultContainer();
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Unexpected error: {ex.Message}");
                // Silently create default container without prompting
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
                
                UnsavedChangesChanged?.Invoke(true);
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
                UnsavedChangesChanged?.Invoke(false);
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
            UnsavedChangesChanged?.Invoke(false);
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
