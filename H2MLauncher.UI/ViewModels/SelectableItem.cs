using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public partial class SelectableItem<T> : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _name = "";

        [JsonIgnore]
        public T Model { get; }

        public SelectableItem(T model) => Model = model;
    }
}
