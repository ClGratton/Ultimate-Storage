using StorageHandler.Scripts;
using System.Diagnostics;
using System.Windows;

namespace StorageHandler.Scripts {
    public class ButtonClickHandlers {
        private readonly ColorEditor _colorEditor;
        private readonly StorageDisplayManager _displayManager;

        public ButtonClickHandlers(ColorEditor colorEditor, StorageDisplayManager displayManager) {
            _colorEditor = colorEditor;
            _displayManager = displayManager;
        }

        public void EditColor_Click(object sender, RoutedEventArgs e) {
            Debug.WriteLine("EditColor_Click in ButtonClickHandlers triggered."); // Debug statement
            _colorEditor.EditColor("Box1", "#FFD700"); // Example: Change Box1's color to gold
        }





        public void SaveChanges_Click() {
            _colorEditor.SaveChanges();
            _displayManager.LoadAndDisplayStorage();
        }

        public void RevertChanges_Click() {
            _colorEditor.RevertChanges();
            _displayManager.LoadAndDisplayStorage(); // Reload the storage data
        }
    }
}


