// Only STATIC configuration constants should go here.

namespace StorageHandler.Config {
    public static class AppConfig {
        //Windows Constants
        public const double DefaultWindowWidth = 920;
        public const double DefaultWindowHeight = 600;
        public const string DefaultStoragePath = "Database\\Storage";
        
    
        // Grid & Canvas Constants
        public const double CanvasScaleFactor = 100.0;
        public const int MaxGridWidth = 20;
        public const int MaxGridHeight = 10;
        public const int MinGridSize = 1;
        
        // UI Interaction Constants
        public const double HandleVisualSize = 16.0;
        public const double DragPixelThreshold = 2.0;
        public const double WindowCaptionHeight = 38.0;
        
        // Box Display Constants
        public const double BoxCornerRadius = 10.0;
        public const double BoxTextMargin = 8.0;
        public const double BoxBorderThickness = 1.0;

        // DataGrid Column Widths
        public const double ColWidth_Id = 120.0;
        public const double ColWidth_Value = 100.0;
        public const double ColWidth_ValueSmall = 80.0;
        public const double ColWidth_Package = 80.0;
        public const double ColWidth_Quantity = 60.0;
        public const double ColWidth_Type = 120.0;
        public const double ColWidth_ModelNumber = 150.0;
        public const double ColWidth_Category = 150.0;
        public const double ColWidth_Datasheet = 80.0;

        // Data Handling
        public const int MaxItemsToScanForColumns = 9999;  // Max items to scan to decide what columns to show
        public const int MaxIdGenerationLimit = 99999;

        // Theme Colors (Background, Text)
        public static readonly (string Background, string Text)[] BoxColorSchemes = new[] {
            ("#FFD6A5", "#000000"), // peach - black text
            ("#FFADAD", "#000000"), // light coral - black text
            ("#FFD6FF", "#000000"), // light pink - black text
            ("#FFFACD", "#000000"), // lemon chiffon - black text
            ("#A0C4FF", "#000000"), // baby blue - black text
            ("#9BF6FF", "#000000"), // cyan - black text
            ("#CAFFBF", "#000000"), // mint - black text
            ("#FFC6FF", "#000000")  // pink - black text
        };

        private static readonly System.Random _random = new System.Random();

        public static (string Background, string Text) GetRandomBoxColor(System.Collections.Generic.List<string>? usedColors = null) {
            if (usedColors == null || usedColors.Count == 0) {
                var scheme = BoxColorSchemes[_random.Next(BoxColorSchemes.Length)];
                return scheme;
            }

            // Find available colors (not yet used on this level)
            var availableSchemes = BoxColorSchemes.Where(s => !usedColors.Contains(s.Background)).ToArray();
            
            // If all colors are used, allow reuse (pick from all)
            if (availableSchemes.Length == 0) {
                availableSchemes = BoxColorSchemes;
            }

            return availableSchemes[_random.Next(availableSchemes.Length)];
        }

        public static string GetTextColorForBackground(string backgroundColor) {
            var scheme = BoxColorSchemes.FirstOrDefault(s => s.Background.Equals(backgroundColor, System.StringComparison.OrdinalIgnoreCase));
            return scheme.Text ?? "#000000"; // Default to black if color not found
        }
    }
}
