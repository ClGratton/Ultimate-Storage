using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using StorageHandler.Models;
using StorageHandler.Helpers;

namespace StorageHandler.Views {
    public partial class ManageCategoriesWindow : Window {
        private readonly ObservableCollection<ComponentDefinition> _categories;

        private string GetStr(string key) {
            return StorageHandler.Properties.Resources.ResourceManager.GetString(key) ?? key;
        }

        public ManageCategoriesWindow(ObservableCollection<ComponentDefinition> categories) {
            InitializeComponent();
            _categories = categories;
            CategoriesList.ItemsSource = _categories;
        }

        private void Add_Click(object sender, RoutedEventArgs e) {
            string name = NewCategoryBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            if (_categories.Any(c => c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))) {
                MessageBox.Show(GetStr("Str_CategoryExists"), GetStr("Str_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _categories.Add(new ComponentDefinition { Name = name });
            NewCategoryBox.Text = string.Empty;
        }

        private void Delete_Click(object sender, RoutedEventArgs e) {
            if (CategoriesList.SelectedItem is ComponentDefinition selected) {
                var result = MessageBox.Show(string.Format(GetStr("Str_ConfirmDeleteCategorySimple"), selected.Name), GetStr("Str_ConfirmDeleteCategory"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) {
                    _categories.Remove(selected);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
