using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StorageHandler.Config;
using StorageHandler.Config.Constants;
using StorageHandler.Models;

namespace StorageHandler.Views {
    public partial class ColorPickerWindow : Window {
        private StorageContainer _container;
        private string? _selectedColor;

        public string? SelectedColor => _selectedColor;

        public ColorPickerWindow(StorageContainer container) {
            InitializeComponent();
            _container = container;
            LoadPresetColors();
            
            // Set text after UI is initialized to avoid null reference in UpdateHexPreview
            HexInput.TextChanged -= HexInput_TextChanged;
            HexInput.Text = container.Color;
            HexInput.TextChanged += HexInput_TextChanged;
            UpdateHexPreview(container.Color);
        }

        private void LoadPresetColors() {
            var colorSchemes = AppConfig.BoxColorSchemes.Select(scheme => new {
                scheme.Background,
                scheme.Text
            }).ToList();

            PresetColorsPanel.ItemsSource = colorSchemes;
        }

        private void PresetColor_Click(object sender, MouseButtonEventArgs e) {
            if (sender is Border border && border.Tag is string colorHex) {
                _selectedColor = colorHex;
                DialogResult = true;
                Close();
            }
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e) {
            var hex = HexInput.Text.Trim();
            UpdateHexPreview(hex);
        }

        private void UpdateHexPreview(string hex) {
            if (HexPreview == null) return;
            
            try {
                if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9)) {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    HexPreview.Background = new SolidColorBrush(color);
                } else {
                    HexPreview.Background = Brushes.Transparent;
                }
            } catch {
                HexPreview.Background = Brushes.Transparent;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e) {
            var hex = HexInput.Text.Trim();
            try {
                if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9)) {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    _selectedColor = hex;
                    DialogResult = true;
                    Close();
                } else {
                    MessageBox.Show("Please enter a valid hex color (e.g., #FF5733)", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            } catch {
                MessageBox.Show("Please enter a valid hex color (e.g., #FF5733)", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
