using System.Text.Json.Serialization;

namespace StorageHandler.Config {
    public class UserConfig {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en-US";  //en-US or it-IT

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark";

        [JsonPropertyName("appDataPath")]
        public string AppDataPath { get; set; } = ""; // Placeholder for now
    }
}
