using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using StorageHandler.Models;
using StorageHandler.Scripts;

namespace StorageHandler.Views {
    public partial class ManageCategoriesWindow : Window {
        private readonly ObservableCollection<ComponentDefinition> _categories;

        public ManageCategoriesWindow(ObservableCollection<ComponentDefinition> categories) {
            InitializeComponent();
            _categories = categories;
            CategoriesList.ItemsSource = _categories;
        }

        private void Add_Click(object sender, RoutedEventArgs e) {
            string name = NewCategoryBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            if (_categories.Any(c => c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))) {
                MessageBox.Show("Category already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _categories.Add(new ComponentDefinition { Name = name });
            NewCategoryBox.Text = string.Empty;
        }

        private void Delete_Click(object sender, RoutedEventArgs e) {
            if (CategoriesList.SelectedItem is ComponentDefinition selected) {
                var result = MessageBox.Show($"Are you sure you want to delete '{selected.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) {
                    _categories.Remove(selected);
                }
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e) {
            var common = DatabaseSeeder.GetCommonCategories();
            int added = 0;
            foreach (var cat in common) {
                if (!_categories.Any(c => c.Name.Equals(cat.Name, System.StringComparison.OrdinalIgnoreCase))) {
                    _categories.Add(cat);
                    added++;
                }
            }
            MessageBox.Show($"Imported {added} new categories.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
