using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StorageHandler.Models {
    public class ComponentModel : INotifyPropertyChanged {
        private string _category = string.Empty;
        private string _type = string.Empty;
        private string _value = string.Empty;
        private string _description = string.Empty;
        private string _modelNumber = string.Empty;
        private string _datasheetLink = string.Empty;
        private Dictionary<string, string> _customData = new Dictionary<string, string>();

        [JsonPropertyName("category")]
        public string Category {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("type")]
        public string Type {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("value")]
        public string Value {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("description")]
        public string Description {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("modelNumber")]
        public string ModelNumber {
            get => _modelNumber;
            set { _modelNumber = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("datasheetLink")]
        public string DatasheetLink {
            get => _datasheetLink;
            set { _datasheetLink = value; OnPropertyChanged(); }
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

        [JsonPropertyName("quantity")]
        public int Quantity {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }
    }
}
