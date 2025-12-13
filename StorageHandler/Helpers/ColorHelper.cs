using StorageHandler.Config.Constants;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageHandler.Helpers {
    public static class ColorHelper {
        private static readonly Random _random = new Random();

        public static (string Background, string Text) GetRandomBoxColor(List<string>? usedColors = null) {
            if (usedColors == null || usedColors.Count == 0) {
                var scheme = AppConfig.BoxColorSchemes[_random.Next(AppConfig.BoxColorSchemes.Length)];
                return scheme;
            }

            // Find available colors (not yet used on this level)
            var availableSchemes = AppConfig.BoxColorSchemes.Where(s => !usedColors.Contains(s.Background)).ToArray();
            
            // If all colors are used, allow reuse (pick from all)
            if (availableSchemes.Length == 0) {
                availableSchemes = AppConfig.BoxColorSchemes;
            }

            return availableSchemes[_random.Next(availableSchemes.Length)];
        }

        public static string GetTextColorForBackground(string backgroundColor) {
            var scheme = AppConfig.BoxColorSchemes.FirstOrDefault(s => s.Background.Equals(backgroundColor, StringComparison.OrdinalIgnoreCase));
            return scheme.Text ?? "#000000"; // Default to black if color not found
        }
    }
}
