using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StorageHandler.Config;

namespace StorageHandler.Views {
    public partial class NewCategoryWindow : Window {
        public string CategoryName { get; private set; } = string.Empty;
        public string ItemsFilePath { get; private set; } = string.Empty;
        public string IdColumn { get; private set; } = string.Empty;

        public NewCategoryWindow() {
            InitializeComponent();
            
            // Default to empty so user must select a file
            ItemsFileBox.Text = string.Empty;
        }

        private void Browse_Click(object sender, RoutedEventArgs e) {
            string storageDir = ConfigManager.StorageDirectory;
            string dbRoot = Path.GetDirectoryName(storageDir) ?? string.Empty;
            string componentsDir = Path.Combine(dbRoot, "Components");

            var dialog = new OpenFileDialog {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                InitialDirectory = componentsDir
            };

            if (dialog.ShowDialog() == true) {
                ItemsFileBox.Text = dialog.FileName;
            }
        }

        private void ItemsFileBox_TextChanged(object sender, TextChangedEventArgs e) {
            string path = ItemsFileBox.Text.Trim();
            if (File.Exists(path)) {
                AnalyzeFile(path);
            } else {
                IdSelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AnalyzeFile(string path) {
            try {
                var json = File.ReadAllText(path);
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array) {
                        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var item in root.EnumerateArray().Take(AppConfig.MaxItemsToScanForColumns)) {
                            foreach (var prop in item.EnumerateObject()) {
                                keys.Add(prop.Name);
                            }
                        }

                        IdColumnCombo.ItemsSource = keys.OrderBy(k => k);
                        IdSelectionPanel.Visibility = Visibility.Visible;
                        
                        // Auto-select common ID names
                        if (keys.Contains("id")) IdColumnCombo.SelectedItem = keys.First(k => k.Equals("id", StringComparison.OrdinalIgnoreCase));
                        else if (keys.Contains("modelNumber")) IdColumnCombo.SelectedItem = keys.First(k => k.Equals("modelNumber", StringComparison.OrdinalIgnoreCase));
                    }
                }
            } catch {
                IdSelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void GenerateIds_Click(object sender, RoutedEventArgs e) {
            string path = ItemsFileBox.Text.Trim();
            if (!File.Exists(path)) return;

            try {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                if (items != null) {
                    // Safety check: Prevent modifying existing 'id' columns
                    if (items.Any(i => i.ContainsKey("id"))) {
                        MessageBox.Show("Column 'id' already exists in the database. Please select it as the Unique ID column, or rename it in your database file if you wish to generate new IDs.", "ID Column Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int counter = 0;
                    foreach (var item in items) {
                        if (counter > AppConfig.MaxIdGenerationLimit) {
                            MessageBox.Show($"ID Limit Reached ({AppConfig.MaxIdGenerationLimit}). Stopping generation.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                            break;
                        }
                        item["id"] = $"{counter:D5}";
                        counter++;
                    }

                    var newJson = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, newJson);
                    
                    MessageBox.Show("IDs generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    AnalyzeFile(path); // Refresh
                    
                    // Select "id"
                    foreach (string item in IdColumnCombo.Items) {
                        if (item.Equals("id", StringComparison.OrdinalIgnoreCase)) {
                            IdColumnCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show($"Failed to generate IDs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e) {
            string name = CategoryNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) {
                MessageBox.Show("Please enter a category name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string path = ItemsFileBox.Text.Trim();
            if (string.IsNullOrEmpty(path)) {
                MessageBox.Show("Please select an items database file.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IdSelectionPanel.Visibility == Visibility.Visible && IdColumnCombo.SelectedItem == null) {
                 MessageBox.Show("Please select a Unique ID column.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }

            CategoryName = name;

            // Copy file to Database/Components if it's not already there
            string fileName = Path.GetFileName(path);
            string storageDir = ConfigManager.StorageDirectory;
            string dbRoot = Path.GetDirectoryName(storageDir) ?? string.Empty;
            string destDir = Path.Combine(dbRoot, "Components");
            string destPath = Path.Combine(destDir, fileName);

            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            if (!string.Equals(path, destPath, StringComparison.OrdinalIgnoreCase)) {
                try {
                    File.Copy(path, destPath, true);
                } catch (Exception ex) {
                    MessageBox.Show($"Failed to import database file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            ItemsFilePath = fileName; // Store relative path (filename only)
            IdColumn = IdColumnCombo.SelectedItem?.ToString() ?? "id";
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
