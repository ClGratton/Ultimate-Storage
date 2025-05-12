using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using StorageHandler.Models;
using StorageHandler.Scripts;


namespace StorageHandler.Views {
    public partial class MainWindow : Window {
        private readonly StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private readonly StorageDisplayManager _displayManager;
        private readonly ColorEditor _colorEditor;
        private StorageContainer? _rootContainer;

        public MainWindow() {
            InitializeComponent();

            string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", "storage1.json");
            Debug.WriteLine($"MainWindow: JSON path resolved to: {jsonPath}");

            try {
                _storageLoader = new StorageLoader(jsonPath);
                _boxManager = new StorageBoxManager(StorageGrid);
                _displayManager = new StorageDisplayManager(_storageLoader, _boxManager, StorageGrid);

                _rootContainer = _storageLoader.LoadStorage();
                if (_rootContainer == null) {
                    throw new Exception("Root container is null after loading storage.");
                }

                _boxManager.InitializeResizer(_storageLoader, _rootContainer);
                _colorEditor = new ColorEditor(_storageLoader, _boxManager, _rootContainer);

                LoadAndDisplayStorage();
            } catch (FileNotFoundException ex) {
                MessageBox.Show($"Error: {ex.Message}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            } catch (JsonException ex) {
                MessageBox.Show($"Error: {ex.Message}", "JSON Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            } catch (Exception ex) {
                MessageBox.Show($"Unexpected Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void LoadAndDisplayStorage() {
            if (_rootContainer == null) {
                Debug.WriteLine("LoadAndDisplayStorage: Root container is null. Exiting.");
                MessageBox.Show("Failed to load storage data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            Debug.WriteLine("LoadAndDisplayStorage: Displaying storage data.");
            _displayManager.LoadAndDisplayStorage();
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e) {
            if (_rootContainer == null) {
                MessageBox.Show("No data to save.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _colorEditor.SaveChanges();
            MessageBox.Show("Changes saved successfully.", "Save Changes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            _colorEditor.RevertChanges();
            _rootContainer = _storageLoader.LoadStorage();
            LoadAndDisplayStorage();
            MessageBox.Show("Changes reverted successfully.", "Revert Changes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                Point mousePosition = e.GetPosition(this);

                // Debug print to see Y coordinate
                Debug.WriteLine($"Mouse clicked at Y = {mousePosition.Y}");

                if (mousePosition.Y <= 38) {
                    if (WindowState == WindowState.Maximized) {

                        Point cursorPosition = PointToScreen(mousePosition);

                        WindowState = WindowState.Normal;

                        // Move window so the cursor position is at the same relative X position
                        // where the user clicked, and maintain Y position at the header
                        Left = cursorPosition.X - (mousePosition.X);
                        Top = cursorPosition.Y - mousePosition.Y;

                        DragMove();
                    } else {
                        DragMove();
                    }
                }
            }
        }



    }
}


