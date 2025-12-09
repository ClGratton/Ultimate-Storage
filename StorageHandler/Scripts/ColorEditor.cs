using StorageHandler.Models;
using System.Diagnostics;

namespace StorageHandler.Scripts {
    public class ColorEditor {
        private StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private StorageContainer? _rootContainer;

        public ColorEditor(StorageLoader storageLoader, StorageBoxManager boxManager, StorageContainer? rootContainer) {
            _storageLoader = storageLoader;
            _boxManager = boxManager;
            _rootContainer = rootContainer;

            Debug.WriteLine("ColorEditor initialized.");
            if (_rootContainer == null) {
                Debug.WriteLine("ColorEditor: _rootContainer is null during initialization.");
            }
        }

        public void UpdateLoader(StorageLoader loader) {
            _storageLoader = loader;
        }

        public void UpdateContainer(StorageContainer? container) {
            _rootContainer = container;
        }


    }
}

