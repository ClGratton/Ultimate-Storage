using System.Windows;
using System.Windows.Input;

namespace StorageHandler.Helpers {
    public static class WindowDragHelper {
        public static void DragWindow(Window window, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var mousePos = e.GetPosition(window);

                if (mousePos.Y < 35 || (mousePos.Y < 51 && mousePos.X < 180)) {
                    if (window.WindowState == WindowState.Maximized) {
                        var screenPos = window.PointToScreen(mousePos);
                        var percentX = screenPos.X / SystemParameters.PrimaryScreenWidth;

                        window.WindowState = WindowState.Normal;

                        window.Dispatcher.Invoke(() => {
                            var width = window.ActualWidth;
                            var newLeft = screenPos.X - width * percentX;
                            window.Left = newLeft;
                            window.Top = 0;
                            window.DragMove();
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    } else {
                        window.DragMove();
                    }
                }
            }
        }
    }
}


