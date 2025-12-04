using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StorageHandler.Models;
using StorageHandler.Scripts;

namespace StorageHandler.Views {
    public partial class ItemSelectionWindow : Window {
        private readonly ObservableCollection<ComponentModel> _allModels;
        private readonly ObservableCollection<ComponentDefinition> _components;
        private ICollectionView _modelsView;

        public ComponentModel? SelectedModel { get; private set; }
        public int SelectedQuantity { get; private set; } = 1;

        private string GetStr(string key) {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        public ItemSelectionWindow(ObservableCollection<ComponentModel> models, ObservableCollection<ComponentDefinition> components) {
            InitializeComponent();
            
            _allModels = models;
            _components = components;

            // Setup Grid - Initialize View BEFORE setting filters that trigger events
            _modelsView = CollectionViewSource.GetDefaultView(_allModels);
            _modelsView.Filter = FilterModels;
            
            ModelsGrid.ItemsSource = _modelsView;

            // Generate columns dynamically
            GenerateColumns();
        }

        private void GenerateColumns() {
            ModelsGrid.Columns.Clear();

            // Determine available keys dynamically by scanning the data
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var model in _allModels.Take(50)) {
                if (!string.IsNullOrEmpty(model.Category)) keys.Add("Category");
                
                foreach (var key in model.CustomData.Keys) {
                    keys.Add(key);
                }
            }

            // Sort keys: Id first, then standard, then custom
            var sortedKeys = keys.OrderBy(k => {
                if (k.Equals("Id", StringComparison.OrdinalIgnoreCase)) return 0;
                if (k.Equals("ModelNumber", StringComparison.OrdinalIgnoreCase)) return 1;
                if (k.Equals("Category", StringComparison.OrdinalIgnoreCase)) return 2;
                if (k.Equals("Description", StringComparison.OrdinalIgnoreCase)) return 3;
                if (k.Equals("Value", StringComparison.OrdinalIgnoreCase)) return 4;
                if (k.Equals("Type", StringComparison.OrdinalIgnoreCase)) return 5;
                return 10;
            }).ToList();

            // Create columns
            string defaultSortHeader = "Category";
            if (keys.Contains("id")) defaultSortHeader = "id";
            else if (keys.Contains("ModelNumber")) defaultSortHeader = "ModelNumber";

            foreach (var key in sortedKeys) {
                var column = new DataGridTextColumn {
                    Header = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                };

                if (key == "Category") {
                    column.Binding = new Binding(key);
                } else {
                    column.Binding = new Binding($"CustomData[{key}]");
                }

                // Special width for Description
                if (key == "Description") column.Width = new DataGridLength(2, DataGridLengthUnitType.Star);
                if (key == "Id") column.Width = 120;
                if (key == "Value") column.Width = 80;
                if (key == "Package") column.Width = 80;
                
                if (key.Equals(defaultSortHeader, StringComparison.OrdinalIgnoreCase)) {
                    column.SortDirection = ListSortDirection.Ascending;
                }

                ModelsGrid.Columns.Add(column);
            }

            // Apply default sort
            _modelsView.SortDescriptions.Clear();
            string sortProperty = defaultSortHeader == "Category" ? "Category" : $"CustomData[{defaultSortHeader}]";
            _modelsView.SortDescriptions.Add(new SortDescription(sortProperty, ListSortDirection.Ascending));
        }

        private bool FilterModels(object item) {
            if (item is not ComponentModel model) return false;

            // Search Box (Multi-token search across all attributes)
            if (!string.IsNullOrWhiteSpace(SearchBox.Text)) {
                var searchTerms = SearchBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var term in searchTerms) {
                    bool termFound = false;
                    
                    // Check standard properties
                    if (model.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true;
                    
                    // Check custom data
                    if (!termFound) {
                        foreach (var value in model.CustomData.Values) {
                            if (value.Contains(term, StringComparison.OrdinalIgnoreCase)) {
                                termFound = true;
                                break;
                            }
                        }
                    }
                    
                    if (!termFound) return false; // If any term is missing, it's not a match
                }
            }

            return true;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) {
            _modelsView?.Refresh();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e) {
            SearchBox.Text = string.Empty;
            _modelsView.Refresh();
        }

        private void ManageCategories_Click(object sender, RoutedEventArgs e) {
            var window = new ManageCategoriesWindow(_components);
            window.Owner = this;
            window.ShowDialog();
        }

        private void CreateNewModel_Click(object sender, RoutedEventArgs e) {
            var window = new NewModelWindow(new List<ComponentDefinition>(_components));
            window.Owner = this;
            if (window.ShowDialog() == true) {
                var newModel = window.Result;
                
                if (newModel != null) {
                    // Check if exists
                    string newModelNum = newModel.CustomData.ContainsKey("ModelNumber") ? newModel.CustomData["ModelNumber"] : "";
                    bool exists = false;
                    if (!string.IsNullOrEmpty(newModelNum)) {
                         exists = _allModels.Any(m => m.CustomData.ContainsKey("ModelNumber") && m.CustomData["ModelNumber"] == newModelNum);
                    }

                    if (!exists) {
                        _allModels.Add(newModel);
                        
                        // Select the new model
                        SearchBox.Text = newModelNum;
                        _modelsView.Refresh();
                        GenerateColumns(); // Regenerate columns in case new fields were added
                    } else {
                        MessageBox.Show(GetStr("Str_ModelExists"), GetStr("Str_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e) {
            if (ModelsGrid.SelectedItem is ComponentModel model) {
                string modelNum = model.CustomData.ContainsKey("ModelNumber") ? model.CustomData["ModelNumber"] : "Unknown";
                var result = MessageBox.Show(string.Format(GetStr("Str_ConfirmDeleteModel"), modelNum), GetStr("Str_ConfirmDeleteCategory"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) {
                    _allModels.Remove(model);
                    _modelsView.Refresh();
                }
            }
        }

        private void AddSelected_Click(object sender, RoutedEventArgs e) {
            if (ModelsGrid.SelectedItem is ComponentModel model) {
                if (int.TryParse(QuantityBox.Text, out int qty) && qty > 0) {
                    SelectedQuantity = qty;
                } else {
                    MessageBox.Show(GetStr("Str_InvalidQty"), GetStr("Str_InvalidQtyTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedModel = model;
                DialogResult = true;
                Close();
            } else {
                MessageBox.Show(GetStr("Str_SelectModelMsg"), GetStr("Str_SelectionRequired"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void ModelsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (ModelsGrid.SelectedItem is ComponentModel) {
                AddSelected_Click(sender, e);
            }
        }
    }
}
