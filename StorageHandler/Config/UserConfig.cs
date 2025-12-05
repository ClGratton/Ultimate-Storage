//only DYNAMIC user-specific configuration should go here.

using System.Text.Json.Serialization;

namespace StorageHandler.Config {
    public class UserConfig {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en-US";  //en-US or it-IT

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark";

        [JsonPropertyName("appDataPath")]
        public string AppDataPath { get; set; } = ""; // Placeholder for now

        // Window State
        [JsonPropertyName("windowWidth")]
        public double WindowWidth { get; set; } = AppConfig.DefaultWindowWidth;

        [JsonPropertyName("windowHeight")]
        public double WindowHeight { get; set; } = AppConfig.DefaultWindowHeight;

        [JsonPropertyName("windowLeft")]
        public double WindowLeft { get; set; } = -1;

        [JsonPropertyName("windowTop")]
        public double WindowTop { get; set; } = -1;

        [JsonPropertyName("isMaximized")]
        public bool IsMaximized { get; set; } = false;
    }
}
