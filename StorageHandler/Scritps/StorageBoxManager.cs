using StorageHandler.Models;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace StorageHandler.Scripts {
    public class StorageBoxManager {
        private readonly Canvas _storageGrid;
        private BoxResizer _boxResizer;
        private StorageLoader _storageLoader;
        private StorageContainer _rootContainer;

        public StorageBoxManager(Canvas storageGrid) {
            _storageGrid = storageGrid;
        }

        public void InitializeResizer(StorageLoader storageLoader, StorageContainer rootContainer) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _boxResizer = new BoxResizer(storageLoader, rootContainer);
        }

        public void AddStorageBox(StorageContainer container) {
            var box = new Border {
                Width = container.Size[0] * 100,
                Height = container.Size[1] * 100,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(container.Color)),
                BorderBrush = Brushes.Black,
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(10),
                DataContext = container
            };

            // Calculate position based on container size
            Canvas.SetLeft(box, container.Position[0] * 100);
            Canvas.SetTop(box, container.Position[1] * 100);

            _storageGrid.Children.Add(box);

            // Add resize handle if this is a top-level container (depth 1)
            if (container.Depth == 1 && _boxResizer != null) {
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
    }
}

