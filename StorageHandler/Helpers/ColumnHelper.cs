using System;
using System.Collections.Generic;
using System.Linq;
using StorageHandler.Models;
using StorageHandler.Config.Constants;

namespace StorageHandler.Helpers {
    public static class ColumnHelper {
        
        private static Dictionary<string, List<string>> _cache = new Dictionary<string, List<string>>();

        public static void ClearCache() {
            _cache.Clear();
        }

        public static void InvalidateCache(string key) {
            if (_cache.ContainsKey(key)) _cache.Remove(key);
        }

        public static List<string> GetSortedKeys(IEnumerable<ComponentModel> items, string? cacheKey = null) {
            if (cacheKey != null && _cache.ContainsKey(cacheKey)) {
                return _cache[cacheKey];
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Use the configured limit
            foreach (var item in items.Take(AppConfig.MaxItemsToScanForColumns)) {
                if (!string.IsNullOrEmpty(item.Category)) keys.Add("Category");
                
                foreach (var key in item.CustomData.Keys) {
                    keys.Add(key);
                }

                if (item is StorageItem sItem) {
                    if (!string.IsNullOrEmpty(sItem.Id)) keys.Add("Id");
                    keys.Add("Quantity");
                }
            }

            var result = SortKeys(keys);
            
            if (cacheKey != null) {
                _cache[cacheKey] = result;
            }

            return result;
        }

        public static List<string> SortKeys(IEnumerable<string> keys) {
            return keys.OrderBy(k => {
                if (k.Equals("Id", StringComparison.OrdinalIgnoreCase)) return 0;
                if (k.Equals("ModelNumber", StringComparison.OrdinalIgnoreCase)) return 1;
                if (k.Equals("Category", StringComparison.OrdinalIgnoreCase)) return 2;
                if (k.Equals("Description", StringComparison.OrdinalIgnoreCase)) return 3;
                if (k.Equals("Value", StringComparison.OrdinalIgnoreCase)) return 4;
                if (k.Equals("Type", StringComparison.OrdinalIgnoreCase)) return 5;
                if (k.Equals("Quantity", StringComparison.OrdinalIgnoreCase)) return 99;
                return 10;
            }).ToList();
        }
    }
}
