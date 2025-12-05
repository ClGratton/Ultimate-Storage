using System.Collections.Generic;
using System.Windows;
using StorageHandler.Models;

namespace StorageHandler.Views {
    public partial class NewModelWindow : Window {
        public ComponentModel? Result { get; private set; }

        private string GetStr(string key) {
            return StorageHandler.Properties.Resources.ResourceManager.GetString(key) ?? key;
        }

        public NewModelWindow(List<ComponentDefinition> components) {
            InitializeComponent();
            ComponentCombo.ItemsSource = components;
            if (components.Count > 0) ComponentCombo.SelectedIndex = 0;
        }

        private void Create_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(ModelNumberBox.Text)) {
                MessageBox.Show(GetStr("Str_EnterModelNumber"), GetStr("Str_ValidationError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ComponentCombo.SelectedItem == null) {
                MessageBox.Show(GetStr("Str_SelectCategoryMsg"), GetStr("Str_ValidationError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ComponentCombo.SelectedItem is not ComponentDefinition selectedComponent) {
                MessageBox.Show(GetStr("Str_InvalidComponent"), GetStr("Str_ValidationError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new ComponentModel {
                Category = selectedComponent.Name,
                CustomData = new Dictionary<string, string> {
                    { "ModelNumber", ModelNumberBox.Text.Trim() },
                    { "DatasheetLink", DatasheetBox.Text.Trim() },
                    { "Type", TypeBox.Text.Trim() },
                    { "Value", ValueBox.Text.Trim() },
                    { "Description", DescriptionBox.Text.Trim() }
                }
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}