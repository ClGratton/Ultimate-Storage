using System.Collections.ObjectModel;
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
        private StorageLoader? _storageLoader;
        private readonly StorageBoxManager? _boxManager;
        private readonly StorageDisplayManager? _displayManager;
        private readonly ColorEditor? _colorEditor;
        private StorageContainer? _rootContainer;

        public ObservableCollection<ComponentDefinition> AvailableComponents { get; set; } = new ObservableCollection<ComponentDefinition>();
        public ObservableCollection<ComponentModel> AvailableModels { get; set; } = new ObservableCollection<ComponentModel>();

        private StorageContainer? _currentParentContainer; // Track parent container for navigation
        private StorageContainer? _currentActiveContainer;
        private int _currentDepth = 1; // Start at default depth 1

        public MainWindow() {
            InitializeComponent();

            // Initial load will be handled by the Checked event of the default RadioButton
            // But we need to initialize managers first
            _boxManager = new StorageBoxManager(StorageGrid);
            _boxManager.Box_MouseDoubleClick += Box_MouseDoubleClick;
            
            // We need a temporary loader to initialize DisplayManager and ColorEditor initially
            // or we can defer their full initialization until LoadStorageCategory is called.
            // However, DisplayManager needs a loader in constructor.
            // Let's initialize with default path first.
            string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", "storage1.json");
            _storageLoader = new StorageLoader(jsonPath);
            
            _displayManager = new StorageDisplayManager(_storageLoader, _boxManager, StorageGrid);
            _colorEditor = new ColorEditor(_storageLoader, _boxManager, null); // Root container will be set later

            LoadCustomCategories();

            // The Checked event fires during InitializeComponent, so LoadStorageCategory might have been called already.
            // But _boxManager was null then. So we should call it explicitly if needed, or rely on the event firing after constructor?
            // Actually, InitializeComponent runs before we set _boxManager.
            // So the event handler would have failed or done nothing.
            // We should manually trigger the load for the default category here.
            
            LoadStorageCategory("electronics");
        }

        private void Category_Checked(object sender, RoutedEventArgs e) {
            if (_boxManager == null) return; // Too early

            if (sender is RadioButton rb && rb.Tag is string category) {
                LoadStorageCategory(category);
            }
        }

        private void CategorySettings_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is string category) {
                MessageBox.Show($"Settings for {category} are not yet implemented.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CategoryDelete_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is string category) {
                // Prevent deleting default categories if desired, though user didn't specify.
                // But let's be safe for "electronics" as it's the fallback.
                if (category.Equals("electronics", StringComparison.OrdinalIgnoreCase)) {
                    MessageBox.Show("Cannot delete the default 'Electronics' category.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete the category '{category}'? This will delete all data associated with it.", 
                                             "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes) {
                    DeleteCategory(category);
                }
            }
        }

        private void DeleteCategory(string category) {
            try {
                // 1. Determine filename
                string filename = category == "electronics" ? "storage1.json" : $"storage_{category}.json";
                string fullPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", filename);
                string tempPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", filename.Replace(".json", ".temp.json"));

                // 2. Delete files
                if (File.Exists(fullPath)) File.Delete(fullPath);
                if (File.Exists(tempPath)) File.Delete(tempPath);

                // 3. Remove from UI
                RadioButton? toRemove = null;
                foreach (var child in CategoryStackPanel.Children) {
                    if (child is RadioButton rb && rb.Tag is string tag && tag.Equals(category, StringComparison.OrdinalIgnoreCase)) {
                        toRemove = rb;
                        break;
                    }
                }

                if (toRemove != null) {
                    CategoryStackPanel.Children.Remove(toRemove);
                }

                // 4. Switch to default category if we deleted the current one
                // Since the buttons are only visible on the selected category, we are definitely deleting the current one.
                // So we must switch.
                if (CategoryStackPanel.Children.Count > 0 && CategoryStackPanel.Children[0] is RadioButton defaultRb) {
                    defaultRb.IsChecked = true;
                }

            } catch (Exception ex) {
                MessageBox.Show($"Error deleting category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewCategory_Click(object sender, RoutedEventArgs e) {
            var inputWindow = new InputWindow("New Category", "Enter category name:");
            inputWindow.Owner = this;
            
            if (inputWindow.ShowDialog() == true) {
                string categoryName = inputWindow.InputValue.Trim();
                if (string.IsNullOrEmpty(categoryName)) return;

                // Sanitize for filename
                string safeName = string.Join("", categoryName.Split(System.IO.Path.GetInvalidFileNameChars()));
                
                // Check if category already exists in UI
                foreach (var child in CategoryStackPanel.Children) {
                    if (child is RadioButton rb && rb.Tag is string tag && tag.Equals(safeName.ToLower(), StringComparison.OrdinalIgnoreCase)) {
                        MessageBox.Show("Category already exists!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                string filename = $"storage_{safeName.ToLower()}.json";
                string fullPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", filename);

                if (File.Exists(fullPath)) {
                    MessageBox.Show("Category already exists!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AddCategoryButton(safeName.ToLower(), categoryName);
                
                // Select the new button
                if (CategoryStackPanel.Children[CategoryStackPanel.Children.Count - 2] is RadioButton newRb) {
                    newRb.IsChecked = true;
                }
            }
        }

        private void LoadCustomCategories() {
            string storageDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage");
            if (!Directory.Exists(storageDir)) return;

            var files = Directory.GetFiles(storageDir, "storage_*.json");
            foreach (var file in files) {
                string filename = System.IO.Path.GetFileName(file);
                
                // Skip temp files
                if (filename.Contains(".temp")) continue;

                string category = filename.Replace("storage_", "").Replace(".json", "");
                
                // Skip known hardcoded ones
                if (category.Equals("yugioh", StringComparison.OrdinalIgnoreCase) || 
                    category.Equals("filament", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                // Convert to Title Case for display
                string displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(category);
                AddCategoryButton(category, displayName);
            }
        }

        private void AddCategoryButton(string tag, string displayName) {
            var rb = new RadioButton {
                Content = displayName,
                Height = (double)FindResource("BaseControlHeight"),
                GroupName = "StorageType",
                Foreground = Brushes.White,
                FontSize = 14,
                Style = (Style)FindResource("MenuButtonTheme"),
                Tag = tag
            };
            
            rb.Checked += Category_Checked;

            // Add to StackPanel before the "New Category" button
            int index = CategoryStackPanel.Children.Count - 1;
            CategoryStackPanel.Children.Insert(index, rb);
        }

        private void LoadStorageCategory(string category) {
            string filename;
            if (category == "electronics") filename = "storage1.json";
            else filename = $"storage_{category}.json";

            string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", filename);
            Debug.WriteLine($"MainWindow: Switching category to {category}, path: {jsonPath}");

            if (_storageLoader != null) {
                _storageLoader.UnsavedChangesChanged -= OnUnsavedChangesChanged;
            }

            try {
                _storageLoader = new StorageLoader(jsonPath);
                _storageLoader.UnsavedChangesChanged += OnUnsavedChangesChanged;
                
                // Update managers with new loader
                if (_displayManager != null) _displayManager.UpdateLoader(_storageLoader);
                if (_colorEditor != null) _colorEditor.UpdateLoader(_storageLoader);
                if (_boxManager != null) _boxManager.UpdateLoader(_storageLoader);

                _rootContainer = _storageLoader.LoadStorage();
                if (_rootContainer == null) {
                    throw new Exception("Root container is null after loading storage.");
                }

                if (_boxManager != null) _boxManager.InitializeResizer(_storageLoader, _rootContainer);
                if (_colorEditor != null) _colorEditor.UpdateContainer(_rootContainer);

                // Reset navigation state
                _currentParentContainer = null;
                _currentActiveContainer = _rootContainer;
                _currentDepth = 1;

                UpdatePersistenceButtons(_storageLoader.HasUnsavedChanges);

                LoadAndDisplayStorage();
                
                // Load components/models for this category (shared or specific?)
                // Assuming shared for now, but we should reload them to be safe
                ReloadComponentsAndModels();

            } catch (Exception ex) {
                MessageBox.Show($"Error loading category {category}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnsavedChangesChanged(bool hasChanges) {
            Dispatcher.Invoke(() => UpdatePersistenceButtons(hasChanges));
        }

        private void UpdatePersistenceButtons(bool hasChanges) {
            if (SaveButton != null) SaveButton.IsEnabled = hasChanges;
            if (RevertButton != null) RevertButton.IsEnabled = hasChanges;
        }

        private void ReloadComponentsAndModels() {
            if (_storageLoader == null) return;

            AvailableComponents.Clear();
            AvailableModels.Clear();

            var components = _storageLoader.LoadComponents();
            foreach (var comp in components) AvailableComponents.Add(comp);
            
            var models = _storageLoader.LoadModels();
            foreach (var model in models) AvailableModels.Add(model);

            if (AvailableComponents.Count == 0) {
                var commonCategories = DatabaseSeeder.GetCommonCategories();
                foreach (var cat in commonCategories) {
                    AvailableComponents.Add(cat);
                }
                _storageLoader.SaveComponents(new List<ComponentDefinition>(AvailableComponents));
            }
        }

        private void LoadAndDisplayStorage() {
            if (_rootContainer == null || _displayManager == null || _boxManager == null) {
                Debug.WriteLine("LoadAndDisplayStorage: Root container or managers are null. Exiting.");
                MessageBox.Show("Failed to load storage data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            Debug.WriteLine("LoadAndDisplayStorage: Displaying storage data.");

            // Ensure we are in Box View when loading root storage
            StorageGrid.Visibility = Visibility.Visible;
            ItemsGrid.Visibility = Visibility.Collapsed;

            _displayManager.LoadAndDisplayStorage();
            UpdateBackButtonVisibility();

            if (_rootContainer.Children.Count == 0) {
                _boxManager.ShowEmptyStatePrompt(
                    () => AddNewBox(0, 0),
                    () => ConvertToItemList() // Root can be converted? Maybe not usually, but logic allows it.
                );
            }
        }

        private void UpdateBackButtonVisibility() {
            if (BackButton != null) {
                BackButton.Visibility = Visibility.Visible;
                BackButton.IsEnabled = _currentDepth > 1;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) {
            NavigateUp();
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e) {
            if (_rootContainer == null || _storageLoader == null) {
                MessageBox.Show("No data to save.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Save the current root container directly
            _storageLoader.SavePermanent(_rootContainer);
            MessageBox.Show("Changes saved successfully.", "Save Changes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            if (_storageLoader == null) return;

            var result = MessageBox.Show("Are you sure you want to undo all unsaved changes?", "Confirm Undo", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // Revert changes by deleting temp file
            _storageLoader.RevertChanges();
            
            // Reload storage from permanent file
            _rootContainer = _storageLoader.LoadStorage();
            
            // Reset navigation to root to avoid inconsistencies
            _currentParentContainer = null;
            _currentActiveContainer = _rootContainer;
            _currentDepth = 1;
            
            // Update managers
            if (_boxManager != null && _rootContainer != null) {
                _boxManager.InitializeResizer(_storageLoader, _rootContainer);
            }
            if (_colorEditor != null) {
                _colorEditor.UpdateContainer(_rootContainer);
            }

            LoadAndDisplayStorage();
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
                containersToCheck = _rootContainer?.Children ?? Enumerable.Empty<StorageContainer>();
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

            // Only show Add Box menu if the container is NOT empty (if empty, we use the phantom box)
            bool isContainerEmpty = (_currentDepth == 1 && _rootContainer != null && _rootContainer.Children.Count == 0) ||
                                    (_currentDepth > 1 && _currentActiveContainer != null && _currentActiveContainer.Children.Count == 0);

            if (!spaceOccupied && !isContainerEmpty) {
                // Show context menu for adding a box rather than immediately creating one
                ShowAddBoxMenu(point, gridX, gridY);
            } else if (isContainerEmpty && _currentDepth > 1) {
                // If empty but deep, allow navigating up via context menu still?
                // The user said "remove all the container empty right click to add box prompts".
                // But maybe they still want to navigate up?
                // Let's show a minimal menu if they right click empty space in an empty container.
                var contextMenu = new ContextMenu();
                var upMenuItem = new MenuItem { Header = "Navigate Up a Level" };
                upMenuItem.Click += (s, ev) => NavigateUp();
                contextMenu.Items.Add(upMenuItem);
                
                contextMenu.PlacementTarget = StorageGrid;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                contextMenu.IsOpen = true;
            }

            e.Handled = true;
        }



        private void ShowAddBoxMenu(Point point, int gridX, int gridY) {
            var contextMenu = new ContextMenu();

            // Add box option
            var menuItem = new MenuItem { Header = "Add Box" };
            menuItem.Click += (s, e) => AddNewBox(gridX, gridY);
            contextMenu.Items.Add(menuItem);

            // Convert to Item List option (only if empty and not root)
            if (_currentDepth > 1 && _currentActiveContainer != null && _currentActiveContainer.Children.Count == 0) {
                var itemListItem = new MenuItem { Header = "Convert to Item List" };
                itemListItem.Click += (s, e) => ConvertToItemList();
                contextMenu.Items.Add(itemListItem);
            }

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

        private Border? FindParentBorder(UIElement? element) {
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
            if (_rootContainer == null || _storageLoader == null || _boxManager == null) return;

            Debug.WriteLine($"DeleteBox: Starting delete of container {container.Name}");

            // Get the container to remove based on current depth
            StorageContainer? containerToRemove = null;

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
                    if (_currentActiveContainer != null) {
                        // Re-initialize for current container
                        _boxManager.InitializeResizer(_storageLoader, _currentActiveContainer);

                        // Redisplay current container's children
                        foreach (var child in _currentActiveContainer.Children) {
                            _boxManager.AddStorageBox(child);
                        }

                        if (_currentActiveContainer.Children.Count == 0) {
                            _boxManager.ShowEmptyStatePrompt(
                                () => AddNewBox(0, 0),
                                () => ConvertToItemList()
                            );
                        }
                    }
                }

                Debug.WriteLine("DeleteBox: Completed refresh after deletion");
            } else {
                Debug.WriteLine($"DeleteBox: Error - Container {container.Name} not found in container");
            }
        }





        private void AddNewBox(int gridX, int gridY) {
            if (_rootContainer == null || _storageLoader == null || _boxManager == null) return;

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

            // Refresh UI using the standard reload method
            // This ensures the new box is displayed correctly with all handles and logic,
            // reusing the same robust code used for resizing and navigation.
            _boxManager.ReloadAllBoxes();
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
            if (_storageLoader == null || _boxManager == null) return;

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
            if (_rootContainer == null) return;

            Debug.WriteLine($"NavigateToContainer: Root container reloaded, has {_rootContainer.Children.Count} children");

            // Find the current container in the refreshed hierarchy
            StorageContainer? updatedContainer = FindContainerByName(_rootContainer, container.Name);
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

            if (_currentActiveContainer.IsItemContainer) {
                // Switch to Item View
                StorageGrid.Visibility = Visibility.Collapsed;
                ItemsGrid.Visibility = Visibility.Visible;
                
                Debug.WriteLine($"NavigateToContainer: Loading items for {_currentActiveContainer.Name}");
                var items = _storageLoader.LoadItems(_currentActiveContainer.Name);
                _currentActiveContainer.Items = items;
                ItemsGrid.ItemsSource = items;
            } else {
                // Switch to Box View
                StorageGrid.Visibility = Visibility.Visible;
                ItemsGrid.Visibility = Visibility.Collapsed;

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
            }

            // Increment depth after navigating down
            _currentDepth++;
            Debug.WriteLine($"NavigateToContainer: New depth is {_currentDepth}");
            Debug.WriteLine($"STORAGE PATH: {(_currentParentContainer != null ? _currentParentContainer.Name + " > " : "")}{_currentActiveContainer.Name}");

            // Update window title to show current location
            UpdateCurrentLocationDisplay(_currentActiveContainer.Name);
            UpdateBackButtonVisibility();

            // If this container is empty, show the phantom box prompt
            if (_currentActiveContainer.Children.Count == 0) {
                Debug.WriteLine($"NavigateToContainer: Container {_currentActiveContainer.Name} is empty - showing phantom box");
                _boxManager.ShowEmptyStatePrompt(
                    () => AddNewBox(0, 0),
                    () => ConvertToItemList()
                );
            }
        }

        private void NavigateUp() {
            if (_rootContainer == null || _storageLoader == null || _boxManager == null) return;

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
            string? activeContainerName = _currentActiveContainer?.Name;

            // Detailed debug for storage hierarchy before navigation
            Debug.WriteLine("STORAGE HIERARCHY BEFORE NAVIGATION:");
            PrintContainerHierarchy(_rootContainer, 0);

            // Reload from storage to ensure we have the latest data
            _rootContainer = _storageLoader.LoadStorage();
            if (_rootContainer == null) return;

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

            // Ensure we are in Box View (parents are always boxes)
            StorageGrid.Visibility = Visibility.Visible;
            ItemsGrid.Visibility = Visibility.Collapsed;

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
            UpdateCurrentLocationDisplay(_currentActiveContainer == _rootContainer ? "Root" : _currentActiveContainer!.Name);
            UpdateBackButtonVisibility();
            
            // If this container is empty, show the phantom box prompt
            if (_currentActiveContainer != null && _currentActiveContainer.Children.Count == 0) {
                Debug.WriteLine($"NavigateUp: Container {_currentActiveContainer.Name} is empty - showing phantom box");
                _boxManager.ShowEmptyStatePrompt(
                    () => AddNewBox(0, 0),
                    () => ConvertToItemList()
                );
            }

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
        private StorageContainer? FindContainerByName(StorageContainer? root, string name) {
            if (root == null) return null;
            if (root.Name == name) return root;

            foreach (var child in root.Children) {
                var result = FindContainerByName(child, name);
                if (result != null) return result;
            }

            return null;
        }

        // Helper method to find the parent container of a named container
        private StorageContainer? FindParentContainer(StorageContainer? root, string childName) {
            if (root == null) return null;
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

        private void ConvertToItemList() {
            if (_currentActiveContainer == null || _rootContainer == null || _storageLoader == null) return;

            // Update the current instance
            _currentActiveContainer.IsItemContainer = true;
            
            // Ensure the change is reflected in the root container before saving
            // (In case _currentActiveContainer is a detached reference, though it shouldn't be)
            var containerInRoot = FindContainerByName(_rootContainer, _currentActiveContainer.Name);
            if (containerInRoot != null) {
                containerInRoot.IsItemContainer = true;
            }

            _storageLoader.SaveTemporary(_rootContainer);
            
            // Reload to switch view
            NavigateToContainer(_currentActiveContainer);
        }

        private void AddItem_Click(object sender, RoutedEventArgs e) {
            if (_currentActiveContainer == null || !_currentActiveContainer.IsItemContainer || _storageLoader == null) return;

            // Open the Item Selection Window
            var selectionWindow = new ItemSelectionWindow(AvailableModels, AvailableComponents);
            selectionWindow.Owner = this;

            if (selectionWindow.ShowDialog() == true && selectionWindow.SelectedModel != null) {
                var model = selectionWindow.SelectedModel;
                int quantityToAdd = selectionWindow.SelectedQuantity;

                if (_currentActiveContainer.Items == null) _currentActiveContainer.Items = new List<StorageItem>();

                // Check if item already exists
                var existingItem = _currentActiveContainer.Items.FirstOrDefault(i => i.ModelNumber == model.ModelNumber);

                if (existingItem != null) {
                    // Update quantity
                    existingItem.Quantity += quantityToAdd;
                } else {
                    // Add new item
                    var newItem = new StorageItem {
                        Category = model.Category,
                        ModelNumber = model.ModelNumber,
                        Type = model.Type,
                        Value = model.Value,
                        Description = model.Description,
                        DatasheetLink = model.DatasheetLink,
                        Quantity = quantityToAdd
                    };
                    _currentActiveContainer.Items.Add(newItem);
                }
                
                _storageLoader.SaveItems(_currentActiveContainer.Name, _currentActiveContainer.Items);
                
                // Refresh grid
                ItemsGrid.ItemsSource = null;
                ItemsGrid.ItemsSource = _currentActiveContainer.Items;

                // Also save models if any new ones were created in the window
                _storageLoader.SaveModels(new List<ComponentModel>(AvailableModels));
                
                // Save components in case categories were managed
                _storageLoader.SaveComponents(new List<ComponentDefinition>(AvailableComponents));
            } else {
                // Even if cancelled, save components/models in case they were modified in the management windows
                _storageLoader.SaveModels(new List<ComponentModel>(AvailableModels));
                _storageLoader.SaveComponents(new List<ComponentDefinition>(AvailableComponents));
            }
        }



        private void DeleteItem_Click(object sender, RoutedEventArgs e) {
            if (ItemsGrid.SelectedItem is StorageItem item && _currentActiveContainer != null && _storageLoader != null) {
                _currentActiveContainer.Items.Remove(item);
                _storageLoader.SaveItems(_currentActiveContainer.Name, _currentActiveContainer.Items);
                
                // Refresh grid
                ItemsGrid.ItemsSource = null;
                ItemsGrid.ItemsSource = _currentActiveContainer.Items;
            }
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) {
            // Defer save to ensure the binding has updated
            Dispatcher.BeginInvoke(new Action(() => {
                if (e.Row.Item is StorageItem item) {
                    // If Model Number matches a known model, update other fields
                    var model = AvailableModels.FirstOrDefault(m => m.ModelNumber == item.ModelNumber);
                    if (model != null) {
                        // Update fields to match model
                        if (item.Type != model.Type) item.Type = model.Type;
                        if (item.Value != model.Value) item.Value = model.Value;
                        if (item.Description != model.Description) item.Description = model.Description;
                        if (item.DatasheetLink != model.DatasheetLink) item.DatasheetLink = model.DatasheetLink;
                    }
                }

                if (_currentActiveContainer != null && _currentActiveContainer.IsItemContainer && _storageLoader != null) {
                    _storageLoader.SaveItems(_currentActiveContainer.Name, _currentActiveContainer.Items);
                }
            }));
        }



        private void OpenDatasheet_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is StorageItem item) {
                if (!string.IsNullOrEmpty(item.DatasheetLink)) {
                    try {
                        var psi = new ProcessStartInfo {
                            FileName = item.DatasheetLink,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    } catch (Exception ex) {
                        MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e) {
            if (_storageLoader != null && _storageLoader.HasUnsavedChanges) {
                var result = MessageBox.Show("You have unsaved changes. Do you want to save before closing?", 
                                             "Unsaved Changes", 
                                             MessageBoxButton.YesNoCancel, 
                                             MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes) {
                    SaveChanges_Click(sender, e);
                    Application.Current.Shutdown();
                } else if (result == MessageBoxResult.No) {
                    Application.Current.Shutdown();
                }
                // If Cancel, do nothing
            } else {
                Application.Current.Shutdown();
            }
        }

    }
}

