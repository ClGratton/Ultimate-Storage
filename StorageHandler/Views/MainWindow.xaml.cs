using StorageHandler.Models;
using StorageHandler.Scripts;
using StorageHandler.Helpers;
using System.Windows;
using System.Windows.Input;

namespace StorageHandler.Views {
    public partial class MainWindow : Window {
        private readonly StorageBoxManager _boxManager;
        private readonly StorageLoader _storageLoader;
        private readonly StorageDisplayManager _displayManager;
        private readonly ButtonClickHandlers _buttonHandlers;
        private StorageContainer? _rootContainer; // Declare _rootContainer here
        private ColorEditor _colorEditor; // Declare _colorEditor here

        public MainWindow() {
            InitializeComponent();

            string jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Database", "Storage", "storage1.json");
            _storageLoader = new StorageLoader(jsonPath);
            _boxManager = new StorageBoxManager(StorageGrid);
            _displayManager = new StorageDisplayManager(_storageLoader, _boxManager, StorageGrid);

            // Load storage data and initialize _rootContainer
            _rootContainer = _storageLoader.LoadStorage();
            if (_rootContainer == null) {
                MessageBox.Show("Failed to load storage data.");
                Application.Current.Shutdown();
                return;
            }

            // Initialize ColorEditor with the loaded _rootContainer
            _colorEditor = new ColorEditor(_storageLoader, _boxManager, _rootContainer);
            _buttonHandlers = new ButtonClickHandlers(_colorEditor, _displayManager);

            // Display the storage data
            _displayManager.LoadAndDisplayStorage();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) {
            WindowDragHelper.DragWindow(this, e);
        }

        private void EditColor_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("EditColor_Click triggered."); // Debug statement
            _buttonHandlers.EditColor_Click(sender, e); // Pass the event arguments to the handler
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e) {
            _buttonHandlers.SaveChanges_Click();
        }

        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            _buttonHandlers.RevertChanges_Click();
        }
    }
}
