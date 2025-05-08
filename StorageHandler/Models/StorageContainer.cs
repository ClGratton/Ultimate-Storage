using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StorageHandler.Models {
    public class StorageContainer {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("allowed")]
        public required List<string> Allowed { get; set; }

        [JsonPropertyName("depth")]
        public int Depth { get; set; }

        [JsonPropertyName("position")]
        public required List<int> Position { get; set; } // [x, y]

        [JsonPropertyName("size")]
        public required List<int> Size { get; set; } // [width, height]

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FFFFFF"; // Default to white

        [JsonPropertyName("children")]
        public required List<StorageContainer> Children { get; set; }
    }
}



