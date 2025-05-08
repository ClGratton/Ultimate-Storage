using StorageHandler.Models;
using StorageHandler.Scripts;
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
        }

        public void EditColor(string boxName, string newColor) {
            Debug.WriteLine($"EditColor called for box: {boxName}, new color: {newColor}"); // Debug statement
         
            if (_rootContainer == null) {
                Debug.WriteLine("EditColor: _rootContainer is null."); // Debug statement
                return;
            }

            var container = _rootContainer.Children.Find(c => c.Name == boxName);
            if (container != null) {
                Debug.WriteLine($"EditColor: Found container with name: {boxName}"); // Debug statement
                _boxManager.EditBoxColor(container, newColor);
                _storageLoader.SaveTemporary(_rootContainer); // Save changes to the temporary file
            } else {
                Debug.WriteLine($"EditColor: No container found with name: {boxName}"); // Debug statement
            }
        }





        public void SaveChanges() {
            if (_rootContainer != null) {
                _storageLoader.SavePermanent(_rootContainer); // Save changes permanently
            }
        }

        public void RevertChanges() {
            _storageLoader.RevertChanges(); // Revert to the original file
            _boxManager.ClearStorageGrid(); // Clear the UI
        }
    }
}


