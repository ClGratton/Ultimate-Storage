using System.Collections.Generic;
using System.Windows;
using StorageHandler.Models;

namespace StorageHandler.Views {
    public partial class NewModelWindow : Window {
        public ComponentModel? Result { get; private set; }

        public NewModelWindow(List<ComponentDefinition> components) {
            InitializeComponent();
            ComponentCombo.ItemsSource = components;
            if (components.Count > 0) ComponentCombo.SelectedIndex = 0;
        }

        private void Create_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(ModelNumberBox.Text)) {
                MessageBox.Show("Please enter a model number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ComponentCombo.SelectedItem == null) {
                MessageBox.Show("Please select a component category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ComponentCombo.SelectedItem is not ComponentDefinition selectedComponent) {
                MessageBox.Show("Invalid component selection.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new ComponentModel {
                Category = selectedComponent.Name,
                ModelNumber = ModelNumberBox.Text.Trim(),
                DatasheetLink = DatasheetBox.Text.Trim(),
                Type = TypeBox.Text.Trim(),
                Value = ValueBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim()
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