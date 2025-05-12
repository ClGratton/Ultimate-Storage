using StorageHandler.Models;
using System.Diagnostics;

namespace StorageHandler.Scripts {
    public class ColorEditor {
        private readonly StorageLoader _storageLoader;
        private readonly StorageBoxManager _boxManager;
        private readonly StorageContainer? _rootContainer;

        public ColorEditor(StorageLoader storageLoader, StorageBoxManager boxManager, StorageContainer? rootContainer) {
            _storageLoader = storageLoader;
            _boxManager = boxManager;
            _rootContainer = rootContainer;

            Debug.WriteLine("ColorEditor initialized.");
            if (_rootContainer == null) {
                Debug.WriteLine("ColorEditor: _rootContainer is null during initialization.");
            }
        }

        public void EditColor(string boxName, string newColor) {
            Debug.WriteLine($"EditColor called for box: {boxName}, new color: {newColor}");

            if (_rootContainer == null) {
                Debug.WriteLine("EditColor: _rootContainer is null.");
                return;
            }

            var container = _rootContainer.Children.Find(c => c.Name == boxName);
            if (container != null) {
                Debug.WriteLine($"EditColor: Found container with name: {boxName}");
                _boxManager.EditBoxColor(container, newColor);
                Debug.WriteLine($"EditColor: Updated color for box: {boxName} to {newColor}");
                _storageLoader.SaveTemporary(_rootContainer);
                Debug.WriteLine("EditColor: Temporary changes saved.");
            } else {
                Debug.WriteLine($"EditColor: No container found with name: {boxName}");
            }
        }

        public void SaveChanges() {
            Debug.WriteLine("SaveChanges called.");

            if (_rootContainer != null) {
                _storageLoader.SavePermanent(_rootContainer);
                Debug.WriteLine("SaveChanges: Changes saved permanently.");
            } else {
                Debug.WriteLine("SaveChanges: _rootContainer is null. No changes saved.");
            }
        }

        public void RevertChanges() {
            Debug.WriteLine("RevertChanges called.");

            _storageLoader.RevertChanges();
            Debug.WriteLine("RevertChanges: Changes reverted to the original file.");

            _boxManager.ClearStorageGrid();
            Debug.WriteLine("RevertChanges: Storage grid cleared.");
        }
    }
}

