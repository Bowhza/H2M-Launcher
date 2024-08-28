using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ServerFilterViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private bool _showEmpty = true;

        [ObservableProperty]
        private bool _showFull = true;

        [ObservableProperty]
        private bool _showPrivate = true;

        [ObservableProperty]
        private int _maxPing = 999;

        [ObservableProperty]
        private int _minPlayers = 0;

        [ObservableProperty]
        private int _maxPlayers = 32;

        [ObservableProperty]
        private int _maxSlots = 32;

        [ObservableProperty]
        private int[] _maxSlotsItems = [6, 12, 18, 24, 32];

        [ObservableProperty]
        private string _filterText = "";

        [ObservableProperty]
        private ObservableCollection<SelectableItem<IW4MMapPack>> _mapPacks = [];

        [ObservableProperty]
        private ObservableCollection<SelectableItem<IW4MObjectMap>> _gameModes = [];

        public string SelectedMapPacks => $"{MapPacks.Where(x => x.IsSelected).Count()}/{MapPacks.Count}";
        public string SelectedGameModes => $"{GameModes.Where(x => x.IsSelected).Count()}/{GameModes.Count}";

        public ICommand ApplyCommand { get; set; }

        public ICommand ResetCommand { get; set; }

        public ServerFilterViewModel(ResourceSettings resourceSettings)
        {
            ApplyCommand = new RelayCommand(() => CloseCommand.Execute(true), () => CloseCommand.CanExecute(true));
            ResetCommand = new RelayCommand(() =>
            {
                ShowEmpty = true;
                ShowFull = true;
                ShowPrivate = true;
                MaxPing = 999;
                MinPlayers = 1;
                MaxPlayers = 32;
                MaxSlots = 32;
                foreach (SelectableItem<IW4MMapPack> item in MapPacks)
                    item.IsSelected = true;
                foreach (SelectableItem<IW4MObjectMap> item in GameModes)
                    item.IsSelected = true;
            });

            MapPacks = [..resourceSettings.MapPacks
                .Select(mapPack =>
                {
                    SelectableItem<IW4MMapPack> item = new(mapPack)
                    {
                        Name = mapPack.Name,
                        IsSelected = true
                    };

                    item.PropertyChanged += MapPackItem_PropertyChanged;

                    return item;
                })];

            MapPacks.Add(new SelectableItem<IW4MMapPack>(new IW4MMapPack() { Name = "Unknown", Maps = [] })
            {
                Name = "Unknown",
                IsSelected = true
            });

            GameModes = [..resourceSettings.GameTypes
                .Select(gameMode => {
                    SelectableItem<IW4MObjectMap> item = new(gameMode)
                    {
                        Name = gameMode.Alias,
                        IsSelected = true
                    };

                    item.PropertyChanged += GameModeItem_PropertyChanged;

                    return item;
                })];

            GameModes.Add(new SelectableItem<IW4MObjectMap>(new IW4MObjectMap("Unknown", "Unknown"))
            {
                Name = "Unknown",
                IsSelected = true
            });
        }

        private void GameModeItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableItem<IW4MObjectMap>.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedGameModes));
            }
        }

        private void MapPackItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableItem<IW4MMapPack>.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedMapPacks));
            }
        }

        private bool ApplyTextFilter(ServerViewModel server)
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                return true;
            }

            string lowerCaseFilter = FilterText.ToLower();

            if (!server.HostName.Contains(lowerCaseFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        public bool ApplyFilter(ServerViewModel server)
        {
            if (!ApplyTextFilter(server))
            {
                return false;
            }

            if (server.ClientNum > MaxPlayers)
            {
                return false;
            }

            if (server.ClientNum < MinPlayers)
            {
                return false;
            }

            if (server.ClientNum == 0 && !ShowEmpty)
            {
                return false;
            }

            if (server.ClientNum == server.MaxClientNum && !ShowFull)
            {
                return false;
            }

            if (server.MaxClientNum > MaxSlots)
            {
                return false;
            }

            if (server.Ping > MaxPing)
            {
                return false;
            }

            if (server.IsPrivate && !ShowPrivate)
            {
                return false;
            }

            // does the game mode exist?
            SelectableItem<IW4MObjectMap>? gameType = GameModes.FirstOrDefault(gameMode => gameMode.Model.Name.Equals(server.GameType, StringComparison.OrdinalIgnoreCase));
            
            // if it doesn't exist, assume Unknown
            gameType ??= GameModes.First(gameMode => gameMode.Model.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));

            // is it selected?
            if (!gameType.IsSelected)
            {
                return false;
            }

            // does the game mode exist?
            SelectableItem<IW4MMapPack>? map = MapPacks.FirstOrDefault(mapPack => mapPack.Model.Maps.Any(m => m.Name.Equals(server.Map, StringComparison.OrdinalIgnoreCase)));
            
            // if it doesn't exist, assume Unknown
            map ??= MapPacks.First(mapPack => mapPack.Model.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));

            // is it selected?
            if (!map.IsSelected)
            {
                return false;
            }

            return true;
        }
    }
}
