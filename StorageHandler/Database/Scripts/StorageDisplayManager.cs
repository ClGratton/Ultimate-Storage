using StorageHandler.Models;
using StorageHandler.Scripts;
using System.Windows.Controls;
using System.Windows;

namespace StorageHandler.Scripts {
    public class StorageDisplayManager {
        private readonly StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private readonly Canvas _storageGrid;

        public StorageDisplayManager(StorageLoader storageLoader, StorageBoxManager boxManager, Canvas storageGrid) {
            _storageLoader = storageLoader;
            _boxManager = boxManager;
            _storageGrid = storageGrid;
        }

        public void LoadAndDisplayStorage() {
            var rootContainer = _storageLoader.LoadStorage();

            if (rootContainer != null) {
                _storageGrid.Children.Clear(); // Clear the UI
                foreach (var child in rootContainer.Children) {
                    if (child.Depth == 1) {
                        _boxManager.AddStorageBox(child);
                    }
                }
            } else {
                Application.Current.Shutdown(); // Exit the application cleanly if loading fails
            }
        }
    }
}


