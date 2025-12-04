using System.Windows;
using System.Windows.Input;

namespace StorageHandler.Views {
    public partial class InputWindow : Window {
        public string InputValue { get; private set; } = string.Empty;

        public InputWindow(string title, string prompt) {
            InitializeComponent();
            TitleText.Text = title;
            PromptText.Text = prompt;
            InputTextBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            InputValue = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                OK_Click(sender, e);
            } else if (e.Key == Key.Escape) {
                Cancel_Click(sender, e);
            }
        }
    }
}