using System.Text.Json.Serialization;
using StorageHandler.Config.Constants;

namespace StorageHandler.Config.Options {
    public class AppState {
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
