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

        public ItemSelectionWindow(ObservableCollection<ComponentModel> models, ObservableCollection<ComponentDefinition> components) {
            InitializeComponent();
            
            _allModels = models;
            _components = components;

            // Setup Grid - Initialize View BEFORE setting filters that trigger events
            _modelsView = CollectionViewSource.GetDefaultView(_allModels);
            _modelsView.Filter = FilterModels;
            
            // Sort by Model Number by default
            _modelsView.SortDescriptions.Add(new SortDescription("ModelNumber", ListSortDirection.Ascending));
            
            ModelsGrid.ItemsSource = _modelsView;

            RefreshCategoryFilter();
        }

        private void RefreshCategoryFilter() {
            // Setup Filters - Dynamic based on available items
            var categories = _allModels
                .Select(m => m.Category)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .Select(n => new ComponentDefinition { Name = n })
                .ToList();

            var filterComponents = new List<ComponentDefinition> { new ComponentDefinition { Name = "All Categories" } };
            filterComponents.AddRange(categories);
            
            CategoryFilter.ItemsSource = filterComponents;
            CategoryFilter.SelectedIndex = 0;
        }

        private bool FilterModels(object item) {
            if (item is not ComponentModel model) return false;

            // Category Filter
            if (CategoryFilter.SelectedValue is string category && category != "All Categories") {
                if (model.Category != category) return false;
            }

            // Type Filter
            if (!string.IsNullOrWhiteSpace(TypeFilter.Text)) {
                if (string.IsNullOrEmpty(model.Type) || !model.Type.Contains(TypeFilter.Text, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Value Filter
            if (!string.IsNullOrWhiteSpace(ValueFilter.Text)) {
                if (string.IsNullOrEmpty(model.Value) || !model.Value.Contains(ValueFilter.Text, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Search Box (Multi-token search across all attributes)
            if (!string.IsNullOrWhiteSpace(SearchBox.Text)) {
                var searchTerms = SearchBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var term in searchTerms) {
                    bool termFound = false;
                    
                    // Check all relevant properties
                    if (model.ModelNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true;
                    else if (model.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true;
                    else if (model.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true; // Category
                    else if (model.Type?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true;
                    else if (model.Value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) termFound = true;
                    
                    if (!termFound) return false; // If any term is missing, it's not a match
                }
            }

            return true;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) {
            _modelsView?.Refresh();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e) {
            CategoryFilter.SelectedIndex = 0;
            TypeFilter.Text = string.Empty;
            ValueFilter.Text = string.Empty;
            SearchBox.Text = string.Empty;
            _modelsView.Refresh();
        }

        private void ManageCategories_Click(object sender, RoutedEventArgs e) {
            var window = new ManageCategoriesWindow(_components);
            window.Owner = this;
            window.ShowDialog();
            
            // Refresh the filter dropdown after managing categories
            RefreshCategoryFilter();
        }

        private void CreateNewModel_Click(object sender, RoutedEventArgs e) {
            var window = new NewModelWindow(new List<ComponentDefinition>(_components));
            window.Owner = this;
            if (window.ShowDialog() == true) {
                var newModel = window.Result;
                
                if (newModel != null) {
                    // Check if exists
                    if (!_allModels.Any(m => m.ModelNumber == newModel.ModelNumber)) {
                        _allModels.Add(newModel);
                        
                        // Select the new model
                        RefreshCategoryFilter(); // Update filter in case of new category
                        CategoryFilter.SelectedValue = newModel.Category;
                        SearchBox.Text = newModel.ModelNumber;
                        _modelsView.Refresh();
                    } else {
                        MessageBox.Show("Model already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ImportCommonParts_Click(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show("This will import common electronic components (Resistors, Capacitors, LEDs, ICs) into your database. Existing models with the same number will be skipped.\n\nContinue?", "Import Database", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes) {
                var commonModels = DatabaseSeeder.GetCommonModels();
                int addedCount = 0;

                foreach (var model in commonModels) {
                    if (!_allModels.Any(m => m.ModelNumber == model.ModelNumber)) {
                        _allModels.Add(model);
                        addedCount++;
                    }
                }

                // Also import categories
                var commonCategories = DatabaseSeeder.GetCommonCategories();
                foreach (var cat in commonCategories) {
                    if (!_components.Any(c => c.Name == cat.Name)) {
                        _components.Add(cat);
                    }
                }
                
                RefreshCategoryFilter(); // Update filter with new items/categories

                if (addedCount > 0) {
                    MessageBox.Show($"Successfully imported {addedCount} new models and updated categories.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    _modelsView.Refresh();
                } else {
                    MessageBox.Show("No new models were added (all already exist). Categories updated.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e) {
            if (ModelsGrid.SelectedItem is ComponentModel model) {
                var result = MessageBox.Show($"Are you sure you want to delete model {model.ModelNumber}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                    MessageBox.Show("Please enter a valid quantity.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedModel = model;
                DialogResult = true;
                Close();
            } else {
                MessageBox.Show("Please select a model from the list.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
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
