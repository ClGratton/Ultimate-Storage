using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StorageHandler.Models {
    public class StorageContainer {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "container";

        [JsonPropertyName("allowed")]
        public List<string> Allowed { get; set; } = new List<string>();

        [JsonPropertyName("depth")]
        public int Depth { get; set; } = 0;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FFFFFF";

        [JsonPropertyName("position")]
        public int[] Position { get; set; } = new int[2];

        [JsonPropertyName("size")]
        public int[] Size { get; set; } = new int[2] { 1, 1 };

        [JsonPropertyName("children")]
        public List<StorageContainer> Children { get; set; } = new List<StorageContainer>();

        [JsonPropertyName("isItemContainer")]
        public bool IsItemContainer { get; set; } = false;

        [JsonPropertyName("itemsDatabasePath")]
        public string? ItemsDatabasePath { get; set; }

        [JsonIgnore]
        public List<StorageItem> Items { get; set; } = new List<StorageItem>();
    }
}



