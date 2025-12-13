using StorageHandler.Models;
using System.Windows.Controls;
using System.Windows;

namespace StorageHandler.Scripts {
    public class StorageDisplayManager {
        private StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private readonly Canvas _storageGrid;
        private StorageContainer? _rootContainer;

        public StorageDisplayManager(StorageLoader storageLoader, StorageBoxManager boxManager, Canvas storageGrid) {
            _storageLoader = storageLoader;
            _boxManager = boxManager;
            _storageGrid = storageGrid;
        }

        public void UpdateLoader(StorageLoader loader) {
            _storageLoader = loader;
        }

        public void LoadAndDisplayStorage() {
            _rootContainer = _storageLoader.LoadStorage();

            if (_rootContainer != null) {
                _storageGrid.Children.Clear();
                foreach (var child in _rootContainer.Children) {
                    // Show containers at depth 1 (top level)
                    if (child.Depth == 1) {
                        _boxManager.AddStorageBox(child);
                    }
                }
            } else {
                MessageBox.Show("Failed to load storage data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
    }
}



