// Only STATIC configuration constants should go here.

namespace StorageHandler.Config.Constants {
    public static class AppConfig {
        // Configuration File Names
        public const string ConfigFileName = "userconfig.json";
        public const string StateFileName = "appstate.json";

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
        public static readonly (string Background, string Text)[] BoxColorSchemes = {
            ("#FFD6A5", "#000000"), // peach - black text
            ("#FFADAD", "#000000"), // light coral - black text
            ("#FFD6FF", "#000000"), // light pink - black text
            ("#FFFACD", "#000000"), // lemon chiffon - black text
            ("#A0C4FF", "#000000"), // baby blue - black text
            ("#9BF6FF", "#000000"), // cyan - black text
            ("#CAFFBF", "#000000"), // mint - black text
            ("#FFC6FF", "#000000")  // pink - black text
        };
    }
}
