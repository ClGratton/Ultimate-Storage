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

        private StorageContainer _currentParentContainer; // Track parent container for navigation
        private StorageContainer _currentActiveContainer;
        private int _currentDepth = 1; // Start at default depth 1

        public MainWindow() {
            InitializeComponent();

            string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", "storage1.json");
            Debug.WriteLine($"MainWindow: JSON path resolved to: {jsonPath}");

            try {
                _storageLoader = new StorageLoader(jsonPath);
                _boxManager = new StorageBoxManager(StorageGrid);
                _boxManager.Box_MouseDoubleClick += Box_MouseDoubleClick;
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

            _rootContainer = _storageLoader.LoadStorage();
            if (_rootContainer == null) {
                throw new Exception("Root container is null after loading storage.");
            }

            _currentActiveContainer = _rootContainer;
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

            // Get the correct collection to check against based on current depth
            IEnumerable<StorageContainer> containersToCheck;
            if (_currentDepth == 1) {
                containersToCheck = _rootContainer.Children;
            } else {
                containersToCheck = _currentActiveContainer?.Children ?? Enumerable.Empty<StorageContainer>();
            }

            // Check if space is free (no boxes at this grid position)
            bool spaceOccupied = containersToCheck
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

            // Add box option
            var menuItem = new MenuItem { Header = "Add Box" };
            menuItem.Click += (s, e) => AddNewBox(gridX, gridY);
            contextMenu.Items.Add(menuItem);

            // Navigate up option (available when not at top level)
            if (_currentDepth > 1) {
                contextMenu.Items.Add(new Separator());
                var upMenuItem = new MenuItem { Header = "Navigate Up a Level" };
                upMenuItem.Click += (s, e) => NavigateUp();
                contextMenu.Items.Add(upMenuItem);
            }

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

            // Delete option
            var deleteMenuItem = new MenuItem { Header = "Delete Box" };
            deleteMenuItem.Click += (s, e) => DeleteBox(container);
            contextMenu.Items.Add(deleteMenuItem);

            // Navigate into option - show for all containers, not just those with children
            var navigateMenuItem = new MenuItem { Header = "Navigate Into (or Double-Click)" };
            navigateMenuItem.Click += (s, e) => NavigateToContainer(container);
            contextMenu.Items.Add(navigateMenuItem);

            // Navigate up option (available when not at top level)
            if (_currentDepth > 1) {
                var upMenuItem = new MenuItem { Header = "Navigate Up a Level" };
                upMenuItem.Click += (s, e) => NavigateUp();
                contextMenu.Items.Add(upMenuItem);
                contextMenu.Items.Add(new Separator());
            }

            contextMenu.PlacementTarget = border;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        }


        private void DeleteBox(StorageContainer container) {
            Debug.WriteLine($"DeleteBox: Starting delete of container {container.Name}");

            // Get the container to remove based on current depth
            StorageContainer containerToRemove = null;

            if (_currentDepth == 1) {
                // If at top level, remove from root container
                containerToRemove = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
                if (containerToRemove != null) {
                    _rootContainer.Children.Remove(containerToRemove);
                }
            } else if (_currentActiveContainer != null) {
                // If inside a container, remove from current container
                containerToRemove = _currentActiveContainer.Children.FirstOrDefault(c => c.Name == container.Name);
                if (containerToRemove != null) {
                    _currentActiveContainer.Children.Remove(containerToRemove);
                }
            }

            if (containerToRemove != null) {
                // Save changes to temporary file
                _storageLoader.SaveTemporary(_rootContainer);
                Debug.WriteLine($"DeleteBox: Saved to temp file after removing {container.Name}");

                // Clear the canvas before redrawing
                _boxManager.ClearStorageGrid();
                Debug.WriteLine("DeleteBox: Cleared storage grid");

                // Use the correct refresh method based on depth
                if (_currentDepth == 1) {
                    LoadAndDisplayStorage();
                } else {
                    // Re-initialize for current container
                    _boxManager.InitializeResizer(_storageLoader, _currentActiveContainer);

                    // Redisplay current container's children
                    foreach (var child in _currentActiveContainer.Children) {
                        _boxManager.AddStorageBox(child);
                    }
                }

                Debug.WriteLine("DeleteBox: Completed refresh after deletion");
            } else {
                Debug.WriteLine($"DeleteBox: Error - Container {container.Name} not found in container");
            }
        }





        private void AddNewBox(int gridX, int gridY) {
            // Create a new container with default properties
            var newBox = new StorageContainer {
                Name = "Box_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Position = new[] { gridX, gridY },
                Size = new[] { 1, 1 },
                Color = "#808080", // Default gray color
                Depth = _currentDepth // Always use the current depth level
            };

            Debug.WriteLine($"AddNewBox: Created new box {newBox.Name} at position [{gridX},{gridY}] with depth {_currentDepth}");

            // Add to current container instead of always to root
            if (_currentDepth == 1) {
                _rootContainer.Children.Add(newBox);
                Debug.WriteLine($"AddNewBox: Added to root container, now has {_rootContainer.Children.Count} children");
            } else if (_currentActiveContainer != null) {
                _currentActiveContainer.Children.Add(newBox);
                Debug.WriteLine($"AddNewBox: Added to container {_currentActiveContainer.Name}, now has {_currentActiveContainer.Children.Count} children");
            }

            // Save changes
            _storageLoader.SaveTemporary(_rootContainer);
            Debug.WriteLine("AddNewBox: Changes saved to temporary file");

            // Add box to UI and ensure it gets resize handles
            if (_currentDepth == 1) {
                // At root level, use standard display method
                LoadAndDisplayStorage();
            } else {
                // Inside a container, we need to handle resize differently

                // Temporarily set depth to 1 to ensure resize handles are created
                int originalDepth = newBox.Depth;
                newBox.Depth = 1;

                // Add the box to UI
                _boxManager.AddStorageBox(newBox);

                // Restore the proper depth after box is added to UI
                newBox.Depth = originalDepth;

                // No need to manually find the box and add resize handles - the BoxManager
                // has already done this when it saw depth 1 during AddStorageBox
                Debug.WriteLine($"AddNewBox: Added box with proper depth {originalDepth}");
            }
        }






        // Add this method after the LoadAndDisplayStorage() method
        private void Box_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (sender is Border box && box.DataContext is StorageContainer container) {
                // Always navigate, even if empty
                NavigateToContainer(container);
                e.Handled = true;
            }
        }


        private void NavigateToContainer(StorageContainer container) {
            Debug.WriteLine($"NavigateToContainer: Navigating into container {container.Name}");
            Debug.WriteLine($"NAVIGATION DEBUG: CURRENT LEVEL = {_currentDepth}, TARGET = {container.Name}");

            // Print parent-child relationships
            Debug.WriteLine("NAVIGATION DEBUG: CONTAINER HIERARCHY:");
            if (_currentActiveContainer != null) {
                Debug.WriteLine($"  Current Container: {_currentActiveContainer.Name}");
                Debug.WriteLine($"  Parent Container: {_currentParentContainer?.Name ?? "null"}");
                Debug.WriteLine($"  Children Count: {_currentActiveContainer.Children.Count}");
                foreach (var child in _currentActiveContainer.Children) {
                    Debug.WriteLine($"    - Child: {child.Name} at [{child.Position[0]},{child.Position[1]}], size [{child.Size[0]},{child.Size[1]}]");
                }
            }

            // Reload from storage to ensure we have the latest data
            Debug.WriteLine("NavigateToContainer: Reloading from storage...");
            _rootContainer = _storageLoader.LoadStorage();
            Debug.WriteLine($"NavigateToContainer: Root container reloaded, has {_rootContainer.Children.Count} children");

            // Find the current container in the refreshed hierarchy
            StorageContainer updatedContainer = FindContainerByName(_rootContainer, container.Name);
            if (updatedContainer == null) {
                Debug.WriteLine($"NavigateToContainer: ERROR - Could not find container {container.Name} in hierarchy");
                return;
            }
            Debug.WriteLine($"NavigateToContainer: Found container {updatedContainer.Name} with {updatedContainer.Children.Count} children");

            // Find the parent container BEFORE setting the current container
            _currentParentContainer = FindParentContainer(_rootContainer, updatedContainer.Name);
            Debug.WriteLine($"NavigateToContainer: Found parent container: {_currentParentContainer?.Name ?? "null"}");

            // Store current active container as the container we're navigating into
            _currentActiveContainer = updatedContainer;
            Debug.WriteLine($"NavigateToContainer: Set active to {_currentActiveContainer.Name}");

            // Clear the canvas
            _boxManager.ClearStorageGrid();
            Debug.WriteLine("NavigateToContainer: Cleared storage grid");

            // Re-initialize BoxResizer and StorageBoxDrag with the current container
            Debug.WriteLine($"NavigateToContainer: Re-initializing resizer with container {_currentActiveContainer.Name}");
            _boxManager.InitializeResizer(_storageLoader, _currentActiveContainer);

            // Display the container's children (if any)
            Debug.WriteLine($"NavigateToContainer: Adding {_currentActiveContainer.Children.Count} children to display");
            if (_currentActiveContainer.Children.Count > 0) {
                Debug.WriteLine("CONTAINER CONTENTS:");
                foreach (var child in _currentActiveContainer.Children) {
                    Debug.WriteLine($"  - {child.Name} at [{child.Position[0]},{child.Position[1]}], size [{child.Size[0]},{child.Size[1]}], color {child.Color}");

                    // Critical fix: When navigating down, we need to ensure boxes at this level get resize handles
                    child.Depth = 1; // Temporarily set to 1 to ensure resize handles are attached
                    _boxManager.AddStorageBox(child);
                }
            } else {
                Debug.WriteLine("CONTAINER IS EMPTY - No children to display");
            }

            // Increment depth after navigating down
            _currentDepth++;
            Debug.WriteLine($"NavigateToContainer: New depth is {_currentDepth}");
            Debug.WriteLine($"STORAGE PATH: {(_currentParentContainer != null ? _currentParentContainer.Name + " > " : "")}{_currentActiveContainer.Name}");

            // Update window title to show current location
            UpdateCurrentLocationDisplay(_currentActiveContainer.Name);

            // If this container is empty, show a message encouraging users to add boxes
            if (_currentActiveContainer.Children.Count == 0) {
                Debug.WriteLine($"NavigateToContainer: Container {_currentActiveContainer.Name} is empty - ready for new boxes");
                MessageBox.Show($"This container is empty. Right-click anywhere to add boxes.", "Empty Container", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NavigateUp() {
            Debug.WriteLine("NAVIGATION DEBUG: ATTEMPTING TO NAVIGATE UP");
            Debug.WriteLine($"NAVIGATION DEBUG: CURRENT LEVEL = {_currentDepth}, CURRENT CONTAINER = {_currentActiveContainer?.Name ?? "null"}");
            Debug.WriteLine($"NAVIGATION DEBUG: PARENT CONTAINER = {_currentParentContainer?.Name ?? "null"}");

            if (_currentDepth <= 1) {
                Debug.WriteLine("NavigateUp: Already at top level");
                Debug.WriteLine($"NAVIGATION DEBUG: CANNOT NAVIGATE UP - Depth={_currentDepth}");
                return; // Already at top level
            }

            // For depth 2, we're navigating up to the root
            bool navigatingToRoot = (_currentDepth == 2);

            Debug.WriteLine($"NavigateUp: Navigating up from depth {_currentDepth} to {_currentDepth - 1}");
            Debug.WriteLine($"NavigateUp: Current active container: {_currentActiveContainer?.Name}, Parent: {_currentParentContainer?.Name ?? "ROOT"}");

            // Save any changes that might have been made at current level
            _storageLoader.SaveTemporary(_rootContainer);
            Debug.WriteLine("NavigateUp: Saved current state to temporary file");

            // Store references before reloading
            string activeContainerName = _currentActiveContainer?.Name;

            // Detailed debug for storage hierarchy before navigation
            Debug.WriteLine("STORAGE HIERARCHY BEFORE NAVIGATION:");
            PrintContainerHierarchy(_rootContainer, 0);

            // Reload from storage to ensure we have the latest data
            _rootContainer = _storageLoader.LoadStorage();
            Debug.WriteLine($"NavigateUp: Root container reloaded, has {_rootContainer.Children.Count} children at top level");

            // Handle navigation based on current depth
            if (navigatingToRoot) {
                // When navigating to root level
                _currentActiveContainer = _rootContainer;
                _currentParentContainer = null;
                Debug.WriteLine("NavigateUp: Navigating to root level");
            } else if (_currentParentContainer != null) {
                // When navigating to a non-root parent
                string parentName = _currentParentContainer.Name;
                _currentActiveContainer = FindContainerByName(_rootContainer, parentName);
                _currentParentContainer = FindParentContainer(_rootContainer, parentName);

                Debug.WriteLine($"NavigateUp: Set active to {_currentActiveContainer?.Name}, new parent to {_currentParentContainer?.Name ?? "ROOT"}");
            } else {
                // Fallback - should not happen with proper parent tracking
                Debug.WriteLine("NavigateUp: NAVIGATION ERROR - No parent found but not at root level");
                _currentActiveContainer = _rootContainer;
                _currentParentContainer = null;
            }

            // Now that we've updated containers, decrease depth
            _currentDepth--;
            Debug.WriteLine($"NavigateUp: New depth: {_currentDepth}");

            // Clear and redraw the canvas
            _boxManager.ClearStorageGrid();
            Debug.WriteLine("NavigateUp: Cleared storage grid");

            // Display the current container's children
            if (_currentActiveContainer != null) {
                // Re-initialize with the current container
                _boxManager.InitializeResizer(_storageLoader, _currentActiveContainer);

                // Display the current container's children
                Debug.WriteLine($"NavigateUp: Adding {_currentActiveContainer.Children.Count} children to display");
                Debug.WriteLine("CURRENT CONTAINER CONTENTS AFTER NAVIGATION:");
                foreach (var child in _currentActiveContainer.Children) {
                    Debug.WriteLine($"  - {child.Name} at [{child.Position[0]},{child.Position[1]}], size [{child.Size[0]},{child.Size[1]}]");

                    // Set depth to 1 temporarily for resize handles
                    int originalDepth = child.Depth;
                    child.Depth = 1;
                    _boxManager.AddStorageBox(child);
                    child.Depth = originalDepth;
                }
            } else {
                // Fallback to root if something went wrong
                _currentActiveContainer = _rootContainer;
                _currentParentContainer = null;
                _currentDepth = 1;
                LoadAndDisplayStorage();
                Debug.WriteLine("NavigateUp: Active container was null, falling back to root");
            }

            // Update UI
            UpdateCurrentLocationDisplay(_currentActiveContainer == _rootContainer ? "Root" : _currentActiveContainer.Name);
            Debug.WriteLine($"NavigateUp: Navigation completed. Current container: {_currentActiveContainer?.Name ?? "Root"}, Depth: {_currentDepth}");
            Debug.WriteLine($"NAVIGATION DEBUG: NEW STATE - Level={_currentDepth}, Container={_currentActiveContainer?.Name ?? "Root"}, Parent={_currentParentContainer?.Name ?? "null"}");
        }


        // Add this helper method to print the complete container hierarchy for debugging
        private void PrintContainerHierarchy(StorageContainer container, int level) {
            if (container == null) return;

            string indent = new string(' ', level * 2);
            Debug.WriteLine($"{indent}- {container.Name} (Children: {container.Children.Count})");

            foreach (var child in container.Children) {
                PrintContainerHierarchy(child, level + 1);
            }
        }




        // Helper method to find a container by name in the hierarchy
        private StorageContainer FindContainerByName(StorageContainer root, string name) {
            if (root.Name == name) return root;

            foreach (var child in root.Children) {
                var result = FindContainerByName(child, name);
                if (result != null) return result;
            }

            return null;
        }

        // Helper method to find the parent container of a named container
        private StorageContainer FindParentContainer(StorageContainer root, string childName) {
            // Special case: if this is a direct child of root
            foreach (var child in root.Children) {
                if (child.Name == childName) {
                    return root;
                }
            }

            // Search deeper in hierarchy
            foreach (var child in root.Children) {
                var result = FindParentContainer(child, childName);
                if (result != null) {
                    return result;
                }
            }

            return null;
        }

        private void SetNavigationContext() {
            // Debug current state
            Debug.WriteLine($"SetNavigationContext: Current container={_currentActiveContainer?.Name}, Depth={_currentDepth}");

            // If at level 1, we're at root
            if (_currentDepth == 1) {
                _currentParentContainer = null;
                return;
            }

            // Otherwise, find the parent
            if (_currentActiveContainer != null) {
                _currentParentContainer = FindParentContainer(_rootContainer, _currentActiveContainer.Name);

                // If we're at level 2 and can't find a parent, root is the parent
                if (_currentParentContainer == null && _currentDepth == 2) {
                    _currentParentContainer = _rootContainer;
                    Debug.WriteLine($"SetNavigationContext: Set root as parent for level 2 container {_currentActiveContainer.Name}");
                }
            }

            Debug.WriteLine($"SetNavigationContext: Updated parent to {_currentParentContainer?.Name ?? "null"}");
        }

        private void UpdateCurrentLocationDisplay(string containerName) {
            Title = $"Storage Handler - {containerName} (Level {_currentDepth})";
        }



    }
}

