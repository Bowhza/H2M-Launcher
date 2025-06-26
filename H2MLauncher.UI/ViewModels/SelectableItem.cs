using System.Text.Json.Serialization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.ViewModels
{
    public partial class SelectableItem<T> : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isSelectable = true;

        [ObservableProperty]
        private string _name = "";

        [JsonIgnore]
        public T Model { get; }

        public ICommand? RemoveCommand { get; set; }

        public SelectableItem(T model, Action? onRemove = null)
        {
            Model = model;

            if (onRemove != null)
            {
                RemoveCommand = new RelayCommand(onRemove);
            }
        }
    }
}
