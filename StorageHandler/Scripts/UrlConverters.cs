using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StorageHandler.Scripts {
    public class UrlToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string str && !string.IsNullOrEmpty(str)) {
                if (str.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    str.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    str.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class NotUrlToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string str && !string.IsNullOrEmpty(str)) {
                if (str.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    str.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    str.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) {
                    return Visibility.Collapsed;
                }
                return Visibility.Visible;
            }
            // If null or empty, show it (as empty text) or hide? 
            // Usually textblock handles empty string fine. 
            // But if we want to hide the textblock when it IS a url, we return Collapsed.
            // If it is NOT a url (including empty), we return Visible.
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
