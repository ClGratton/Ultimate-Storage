using StorageHandler.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace StorageHandler.Scripts {
    public class StorageLoader {
        private readonly string _originalPath;
        private readonly string _tempPath;
        private readonly string _itemsDirectory;
        private readonly string _componentsPath;

        public string? CustomComponentsPath { get; set; }

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
            
            if (!Directory.Exists(_itemsDirectory)) {
                Directory.CreateDirectory(_itemsDirectory);
            }

            // Generate Test Files (API Emulator)
            ApiEmulator.GenerateTestFiles(databaseDir);

            Debug.WriteLine($"StorageLoader: Initialized with path: {originalPath}");
            Debug.WriteLine($"StorageLoader: Temp path: {_tempPath}");
            Debug.WriteLine($"StorageLoader: Items directory: {_itemsDirectory}");
            Debug.WriteLine($"StorageLoader: Components path: {_componentsPath}");
        }

        public List<ComponentModel> LoadCatalogFromFile(string path) {
            if (!File.Exists(path)) return new List<ComponentModel>();
            try {
                var json = File.ReadAllText(path);
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Array) return new List<ComponentModel>();

                    var items = new List<ComponentModel>();
                    foreach (var element in root.EnumerateArray()) {
                        var item = new ComponentModel();
                        foreach (var prop in element.EnumerateObject()) {
                            string key = prop.Name;
                            string value = prop.Value.ToString();

                            if (key.Equals("category", StringComparison.OrdinalIgnoreCase)) item.Category = value;
                            else item.CustomData[key] = value;
                        }
                        items.Add(item);
                    }
                    return items;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to load catalog from {path}: {ex.Message}");
                return new List<ComponentModel>();
            }
        }

        private string GetPropertyValue(ComponentModel item, string propertyName) {
            if (string.IsNullOrEmpty(propertyName)) return "";
            if (propertyName.Equals("category", StringComparison.OrdinalIgnoreCase)) return item.Category;
            
            if (item.CustomData.TryGetValue(propertyName, out string? val)) return val;
            var key = item.CustomData.Keys.FirstOrDefault(k => k.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (key != null) return item.CustomData[key];
            return "";
        }

        public List<ComponentDefinition> LoadComponents() {
            var components = new List<ComponentDefinition>();
            
            string standardPath = CustomComponentsPath ?? _componentsPath;

            if (File.Exists(standardPath)) {
                try {
                    var json = File.ReadAllText(standardPath);
                    var loaded = JsonSerializer.Deserialize<List<ComponentDefinition>>(json);
                    if (loaded != null) components.AddRange(loaded);
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to load components: {ex.Message}");
                }
            } else if (CustomComponentsPath == null) {
                // First run: Initialize with empty defaults or create file
                SaveComponents(components);
            }

            return components;
        }

        public void SaveComponents(List<ComponentDefinition> components) {
            string standardPath = CustomComponentsPath ?? _componentsPath;

            try {
                var json = JsonSerializer.Serialize(components, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(standardPath, json);
                Debug.WriteLine($"StorageLoader: Saved components to {standardPath}");
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save components: {ex.Message}");
            }
        }

        public List<StorageItem> LoadItems(string containerName) {
            // 1. Get Definition to find Catalog File
            var components = LoadComponents();
            var definition = components.FirstOrDefault(c => c.Name.Equals(containerName, StringComparison.OrdinalIgnoreCase));
            
            List<ComponentModel> catalog = new List<ComponentModel>();
            if (definition != null && !string.IsNullOrEmpty(definition.DatabaseFile)) {
                string catalogPath = Path.Combine(Path.GetDirectoryName(_itemsDirectory) ?? "", "Components", definition.DatabaseFile);
                if (File.Exists(catalogPath)) {
                    catalog = LoadCatalogFromFile(catalogPath);
                }
            }

            // 2. Load Inventory (IDs + Quantities)
            string inventoryPath = Path.Combine(_itemsDirectory, $"storage_{containerName}.json");
            // Fallback for legacy naming
            if (!File.Exists(inventoryPath)) inventoryPath = Path.Combine(_itemsDirectory, $"{containerName}.json");

            if (!File.Exists(inventoryPath)) return new List<StorageItem>();

            try {
                var json = File.ReadAllText(inventoryPath);
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Array) return new List<StorageItem>();

                    var items = new List<StorageItem>();
                    foreach (var element in root.EnumerateArray()) {
                        var item = new StorageItem();
                        
                        string id = "";
                        int quantity = 0;
                        
                        if (element.TryGetProperty("id", out var idProp)) id = idProp.GetString() ?? "";
                        if (element.TryGetProperty("quantity", out var qtyProp)) quantity = qtyProp.GetInt32();

                        // If we have a catalog, look it up
                        if (catalog.Count > 0 && !string.IsNullOrEmpty(id)) {
                            string idCol = definition?.IdColumn ?? "id";
                            
                            // Find item where property[idCol] == id
                            var catalogItem = catalog.FirstOrDefault(c => GetPropertyValue(c, idCol) == id);
                            
                            if (catalogItem != null) {
                                item.Category = catalogItem.Category;
                                item.CustomData = new Dictionary<string, string>(catalogItem.CustomData);
                                item.Id = id;
                            } else {
                                item.Id = id;
                                item.CustomData["Description"] = "Unknown Item (Missing from Catalog)";
                            }
                        } else {
                            // Legacy or No Catalog: Load full object from JSON
                            item = JsonSerializer.Deserialize<StorageItem>(element.GetRawText()) ?? new StorageItem();
                        }

                        item.Quantity = quantity;
                        items.Add(item);
                    }
                    return items;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to load items for {containerName}: {ex.Message}");
                return new List<StorageItem>();
            }
        }

        public void SaveItems(string containerName, List<StorageItem> items) {
            try {
                var components = LoadComponents();
                var definition = components.FirstOrDefault(c => c.Name.Equals(containerName, StringComparison.OrdinalIgnoreCase));
                
                bool useNewSystem = definition != null && !string.IsNullOrEmpty(definition.DatabaseFile);
                
                string filePath = Path.Combine(_itemsDirectory, $"storage_{containerName}.json");
                
                if (useNewSystem) {
                    // Save only ID and Quantity
                    var inventoryItems = items.Select(i => new { id = i.Id, quantity = i.Quantity }).ToList();
                    var json = JsonSerializer.Serialize(inventoryItems, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                } else {
                    // Legacy: Save full object
                    var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                }
                
                Debug.WriteLine($"StorageLoader: Saved items for {containerName}");
            } catch (Exception ex) {
                Debug.WriteLine($"StorageLoader: Failed to save items for {containerName}: {ex.Message}");
            }
        }

        public void RenameItemFile(string oldName, string newName) {
            string oldPath = Path.Combine(_itemsDirectory, $"storage_{oldName}.json");
            string newPath = Path.Combine(_itemsDirectory, $"storage_{newName}.json");
            
            // Also check legacy paths
            if (!File.Exists(oldPath)) oldPath = Path.Combine(_itemsDirectory, $"{oldName}.json");
            
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
            string filePath = Path.Combine(_itemsDirectory, $"storage_{containerName}.json");
            if (File.Exists(filePath)) {
                try {
                    File.Delete(filePath);
                    Debug.WriteLine($"StorageLoader: Deleted items file for {containerName}");
                } catch (Exception ex) {
                    Debug.WriteLine($"StorageLoader: Failed to delete items file for {containerName}: {ex.Message}");
                }
            }
            
            // Legacy
            filePath = Path.Combine(_itemsDirectory, $"{containerName}.json");
            if (File.Exists(filePath)) {
                try {
                    File.Delete(filePath);
                } catch { }
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
            return new StorageContainer {
                Name = "Default",
                Type = "container",
                Allowed = new List<string>(),
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
