using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace StorageHandler.Scripts {
    public class DictionaryValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is Dictionary<string, string> dict && parameter is string key) {
                if (dict.TryGetValue(key, out string? val)) {
                    return val;
                }
            }
            return string.Empty; // Return empty string if key not found, avoiding exception
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
