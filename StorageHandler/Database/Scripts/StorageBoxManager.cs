using StorageHandler.Controls;
using StorageHandler.Models;
using System.Windows.Controls;
using System.Windows.Media;

namespace StorageHandler.Scripts {
    public class StorageBoxManager {
        private readonly Canvas _storageGrid;

        public StorageBoxManager(Canvas storageGrid) {
            _storageGrid = storageGrid;
        }

        public void AddStorageBox(StorageContainer container) {
            // Define the offset per grid unit
            const int offsetPerUnit = 10; // Adjust this value as needed

            // Calculate the width and height with offsets
            double boxWidth = (container.Size[0] * 100) + ((container.Size[0] - 1) * offsetPerUnit);
            double boxHeight = (container.Size[1] * 100) + ((container.Size[1] - 1) * offsetPerUnit);

            // Create a new StorageBox control
            var box = new StorageBox {
                DataContext = new {
                    
                    BoxLabel = container.Name,
                    BoxWidth = boxWidth,
                    BoxHeight = boxHeight,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(container.Color))
                }
            };

            // Position the box dynamically in the Canvas
            double left = container.Position[0] * 110; // X-coordinate in grid units
            double top = container.Position[1] * 110; // Y-coordinate in grid units

            Canvas.SetLeft(box, left);
            Canvas.SetTop(box, top);

            // Add the box to the StorageGrid
            _storageGrid.Children.Add(box);
        }
        public void EditBoxColor(StorageContainer container, string newColor) {
            container.Color = newColor; // Update the color in the model

            // Find the corresponding StorageBox in the Canvas
            foreach (var child in _storageGrid.Children) {
                if (child is StorageBox box && box.DataContext is { } data) {
                    // Check if the BoxLabel matches the container's name
                    var boxLabel = data.GetType().GetProperty("BoxLabel")?.GetValue(data)?.ToString();
                    if (boxLabel == container.Name) {
                        box.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(newColor));
                        break;
                    }
                }
            }
        }
        public void ClearStorageGrid() {
            _storageGrid.Children.Clear();
        }







    }
}

