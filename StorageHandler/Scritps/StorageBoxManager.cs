using StorageHandler.Models;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace StorageHandler.Scripts {
    public class StorageBoxManager {
        private readonly Canvas _storageGrid;
        private BoxResizer _boxResizer;
        private StorageLoader _storageLoader;
        private StorageContainer _rootContainer;
        private StorageBoxDrag _boxDrag;
        public event MouseButtonEventHandler Box_MouseDoubleClick;

        public StorageBoxManager(Canvas storageGrid) {
            _storageGrid = storageGrid;
        }

        public void InitializeResizer(StorageLoader storageLoader, StorageContainer rootContainer) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _boxResizer = new BoxResizer(storageLoader, rootContainer);
            _boxDrag = new StorageBoxDrag(_storageGrid, storageLoader, rootContainer, _boxResizer);
        }

        public void AddStorageBox(StorageContainer container) {
            // Create the main border for the box
            var box = new Border {
                Width = container.Size[0] * 100,
                Height = container.Size[1] * 100,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(container.Color)),
                BorderBrush = Brushes.Black,
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(10),
                DataContext = container
            };

            // Add a TextBlock to display the box name
            var textBlock = new TextBlock {
                Text = container.Name,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            // Add the TextBlock to the Border using a Grid for centering
            var grid = new Grid();
            grid.Children.Add(textBlock);
            box.Child = grid;

            // Calculate position based on container size
            Canvas.SetLeft(box, container.Position[0] * 100);
            Canvas.SetTop(box, container.Position[1] * 100);

            // Add mouse event handlers for drag-and-drop functionality
            _boxDrag.AttachDragHandlers(box, container);

            // Add this line for double-click handling
            box.MouseLeftButtonDown += (sender, e) => {
                if (e.ClickCount == 2) {
                    Box_MouseDoubleClick?.Invoke(sender, e);
                }
            };

            _storageGrid.Children.Add(box);

            // Use the unified method to add resize handle if appropriate
            TryAddResizeHandle(box, container);
        }

        /// <summary>
        /// Adds a resize handle to a box if appropriate based on container properties.
        /// All resize handle logic is consolidated in this single method.
        /// </summary>
        private void TryAddResizeHandle(Border box, StorageContainer container) {
            // Add resize handles to containers at the current display level (Depth == 1)
            // or those specifically marked for resize capability (Depth == 0)
            if (_boxResizer != null && (container.Depth == 1 || container.Depth == 0)) {
                Debug.WriteLine($"Adding resize handle to {container.Name} with depth {container.Depth}");
                _boxResizer.AttachResizeHandle(box, container);
            }
        }

        public void EditBoxColor(StorageContainer container, string newColor) {
            container.Color = newColor;

            foreach (var child in _storageGrid.Children) {
                if (child is Border box && box.DataContext == container) {
                    box.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(newColor));
                    break;
                }
            }
        }

        public void ClearStorageGrid() {
            _storageGrid.Children.Clear();
            if (_boxResizer != null) {
                _boxResizer.ClearHandles(_storageGrid);
            }
        }

        public StorageContainer GetRootContainer() {
            return _rootContainer;
        }

        public void RefreshBoxes() {
            // Clear the current UI
            ClearStorageGrid();

            // Re-add all boxes from the root container
            if (_rootContainer != null) {
                foreach (var container in _rootContainer.Children) {
                    if (container.Depth == 1) { // Only display top-level containers
                        AddStorageBox(container);
                    }
                }
            }
        }

        // This method is kept for backward compatibility with existing code
        public void sAddResizeHandleToBox(Border box, StorageContainer container) {
            TryAddResizeHandle(box, container);
        }
    }
}
