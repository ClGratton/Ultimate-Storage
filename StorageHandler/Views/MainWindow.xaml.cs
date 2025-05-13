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
        private BoxResizer _boxResizer;

        private bool _isBoxDragging = false;
        private Point _boxDragStartPoint;
        private Border _draggedBox;
        private StorageContainer _draggedContainer;
        private int[] _originalBoxPosition;
        private Canvas _dragCanvas;

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

        private void StorageGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            Debug.WriteLine("Right click detected on StorageGrid");
            var point = e.GetPosition(StorageGrid);

            // First check if we clicked on a box
            var hitElement = StorageGrid.InputHitTest(point) as UIElement;
            var border = FindParentBorder(hitElement);

            // If we clicked on a box, show delete context menu
            if (border != null && border.DataContext is StorageContainer container) {
                ShowDeleteBoxMenu(border, container, point);
                e.Handled = true;
                return;
            }

            // Otherwise, check if we can add a box at the clicked position
            int gridX = (int)(point.X / BoxResizer.CanvasScaleFactor);
            int gridY = (int)(point.Y / BoxResizer.CanvasScaleFactor);

            // Check if space is free (no boxes at this grid position)
            bool spaceOccupied = _rootContainer.Children
                .Any(box =>
                    gridX >= box.Position[0] &&
                    gridX < box.Position[0] + box.Size[0] &&
                    gridY >= box.Position[1] &&
                    gridY < box.Position[1] + box.Size[1]);

            if (!spaceOccupied) {
                // Show context menu for adding a box rather than immediately creating one
                ShowAddBoxMenu(point, gridX, gridY);
            }

            e.Handled = true;
        }


        private void ShowAddBoxMenu(Point point, int gridX, int gridY) {
            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem { Header = "Add Box" };
            menuItem.Click += (s, e) => AddNewBox(gridX, gridY);
            contextMenu.Items.Add(menuItem);
            contextMenu.PlacementTarget = StorageGrid;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        }

        private Border FindParentBorder(UIElement element) {
            while (element != null && !(element is Canvas) && !(element is Border)) {
                element = VisualTreeHelper.GetParent(element) as UIElement;
            }
            return element as Border;
        }

        private void ShowDeleteBoxMenu(Border border, StorageContainer container, Point point) {
            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem { Header = "Delete Box" };
            menuItem.Click += (s, e) => DeleteBox(container);
            contextMenu.Items.Add(menuItem);
            contextMenu.PlacementTarget = border;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        }

        private void DeleteBox(StorageContainer container) {
            // Debug to track the deletion process
            Debug.WriteLine($"DeleteBox: Starting delete of container {container.Name}");

            // Get the actual instance from the root container
            var containerToRemove = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (containerToRemove != null) {
                // Remove from data model
                _rootContainer.Children.Remove(containerToRemove);

                // Save changes to temporary file
                _storageLoader.SaveTemporary(_rootContainer);
                Debug.WriteLine($"DeleteBox: Saved to temp file after removing {container.Name}");

                // Clear the canvas before redrawing
                _boxManager.ClearStorageGrid();
                Debug.WriteLine("DeleteBox: Cleared storage grid");

                // Use the existing display refresh method
                LoadAndDisplayStorage();
                Debug.WriteLine("DeleteBox: Completed refresh after deletion");
            } else {
                Debug.WriteLine($"DeleteBox: Error - Container {container.Name} not found in root container");
            }
        }




        private void AddNewBox(int gridX, int gridY) {
            // Create a new container with default properties
            var newBox = new StorageContainer {
                Name = "Box_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Position = new[] { gridX, gridY },
                Size = new[] { 1, 1 },
                Color = "#808080", // Default gray color
                Depth = 1 // Set to 1 since we're only displaying top-level containers (depth 1)
            };

            // Add to root container and save
            _rootContainer.Children.Add(newBox);
            _storageLoader.SaveTemporary(_rootContainer);

            // Refresh UI
            LoadAndDisplayStorage();
        }


    }
}

