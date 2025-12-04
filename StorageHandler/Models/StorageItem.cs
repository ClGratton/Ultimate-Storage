using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StorageHandler.Models {
    public class ComponentModel : INotifyPropertyChanged {
        private string _category = string.Empty;
        private Dictionary<string, string> _customData = new Dictionary<string, string>();

        [JsonPropertyName("category")]
        public string Category {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("customData")]
        public Dictionary<string, string> CustomData {
            get => _customData;
            set { _customData = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StorageItem : ComponentModel {
        private int _quantity = 0;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }
    }
}
