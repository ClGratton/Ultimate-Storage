using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace StorageHandler.Views {
    public partial class MainWindow : Window {


        public MainWindow() {
            InitializeComponent();

        }

        private void DragWindow(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var mousePos = e.GetPosition(this);

                if (mousePos.Y < 35 || (mousePos.Y < 51 && mousePos.X < 180)) {
                    if (WindowState == WindowState.Maximized) {
                        var screenPos = PointToScreen(mousePos);
                        var percentX = screenPos.X / SystemParameters.PrimaryScreenWidth;

                        WindowState = WindowState.Normal;

                        this.Dispatcher.Invoke(() => {
                            var width = this.ActualWidth;
                            var newLeft = screenPos.X - width * percentX;
                            this.Left = newLeft;
                            this.Top = 0;
                            DragMove();
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    } else {
                        DragMove();
                    }
                }
            }
        }

    }
}
