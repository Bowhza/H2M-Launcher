using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Settings;

namespace H2MLauncher.UI.ViewModels
{
    public partial class MapPackItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _name = "";

        public IW4MMapPack Model { get; }

        public MapPackItem(IW4MMapPack model)
        {
            Model = model;
        }
    }
}
