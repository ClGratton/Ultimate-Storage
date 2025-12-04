using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StorageHandler.Models {
    public class ComponentDefinition {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("customFields")]
        public List<string> CustomFields { get; set; } = new List<string>();

        [JsonPropertyName("databaseFile")]
        public string DatabaseFile { get; set; } = string.Empty;

        [JsonPropertyName("idColumn")]
        public string IdColumn { get; set; } = string.Empty;
    }
}
