using StorageHandler.Models;
using System.Windows.Controls;
using System.Windows;

namespace StorageHandler.Scripts {
    public class StorageDisplayManager {
        private readonly StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private readonly Canvas _storageGrid;
        private StorageContainer _rootContainer;

        public StorageDisplayManager(StorageLoader storageLoader, StorageBoxManager boxManager, Canvas storageGrid) {
            _storageLoader = storageLoader;
            _boxManager = boxManager;
            _storageGrid = storageGrid;
        }

        public void LoadAndDisplayStorage() {
            _rootContainer = _storageLoader.LoadStorage(); // Store the loaded container

            if (_rootContainer != null) {
                _storageGrid.Children.Clear(); // Clear the UI
                foreach (var child in _rootContainer.Children) {
                    // Show containers at depth 1 (top level)
                    if (child.Depth == 1) {
                        _boxManager.AddStorageBox(child);
                    }
                }
            } else {
                MessageBox.Show("Failed to load storage data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(); // Exit the application cleanly if loading fails
            }
        }


        public StorageContainer GetCurrentStorage() {
            return _rootContainer;
        }
    }
}



