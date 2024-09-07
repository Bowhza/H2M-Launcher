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
        private readonly ResourceSettings _resourceSettings;

        [ObservableProperty]
        private bool _showEmpty;

        [ObservableProperty]
        private bool _showFull;

        [ObservableProperty]
        private bool _showPrivate;

        [ObservableProperty]
        private int _maxPing;

        [ObservableProperty]
        private int _minPlayers;

        [ObservableProperty]
        private int _maxPlayers;

        [ObservableProperty]
        private int _maxSlots;

        [ObservableProperty]
        private int[] _maxSlotsItems = [6, 12, 18, 24, 32];

        [ObservableProperty]
        private string _filterText = "";

        [ObservableProperty]
        private ObservableCollection<SelectableItem<IW4MMapPack>> _mapPacks = [];

        [ObservableProperty]
        private ObservableCollection<SelectableItem<IW4MObjectMap>> _gameModes = [];

        [ObservableProperty]
        private ObservableCollection<SelectableItem<string>> _excludeFilters = [];

        public string SelectedMapPacks => $"{MapPacks.Where(x => x.IsSelected).Count()}/{MapPacks.Count}";
        public string SelectedGameModes => $"{GameModes.Where(x => x.IsSelected).Count()}/{GameModes.Count}";

        [ObservableProperty]
        private SelectableItem<IW4MMapPack> _unknownMaps;

        [ObservableProperty]
        private SelectableItem<IW4MMapPack> _notInstalledMaps;

        [ObservableProperty]
        private SelectableItem<IW4MObjectMap> _unknownGameModes;

        public ICommand ApplyCommand { get; set; }
        public ICommand ResetCommand { get; set; }

        public ServerFilterViewModel(ResourceSettings resourceSettings, ServerFilterSettings defaultSettings)
        {
            ApplyCommand = new RelayCommand(() => CloseCommand.Execute(true), () => CloseCommand.CanExecute(true));
            ResetCommand = new RelayCommand(() =>
            {
                // reset to default
                ResetViewModel(defaultSettings);
            });

            _resourceSettings = resourceSettings;

            UnknownMaps = new(new IW4MMapPack() { Name = "Unknown", Id = "unknown", Maps = [] })
            {
                Name = "Unknown",
                IsSelected = true,
            };

            NotInstalledMaps = new(new IW4MMapPack() { Name = "Not installed", Id = "not_installed", Maps = [] })
            {
                Name = "Not installed",
                IsSelected = true,
            };

            UnknownGameModes = new(new IW4MObjectMap("unknown", "Unknown"))
            {
                Name = "Unknown",
                IsSelected = true
            };
        }

        public void ResetViewModel(ServerFilterSettings settings)
        {
            ShowEmpty = settings.ShowEmpty;
            ShowFull = settings.ShowFull;
            ShowPrivate = settings.ShowPrivate;
            MaxPing = settings.MaxPing;
            MinPlayers = settings.MinPlayers;
            MaxPlayers = settings.MaxPlayers;
            MaxSlots = settings.MaxSlots;


            // map packs

            MapPacks.AsParallel().ForAll(item => item.PropertyChanged -= MapPackItem_PropertyChanged);
            MapPacks.Clear();
            
            MapPacks = [.._resourceSettings.MapPacks
                .Select(mapPack =>
                {
                    SelectableItem<IW4MMapPack> item = new(mapPack)
                    {
                        Name = mapPack.Name,
                        IsSelected = settings.MapPacks?.GetValueOrDefault(mapPack.Id, true) ?? true
                    };

                    item.PropertyChanged += MapPackItem_PropertyChanged;

                    return item;
                }), UnknownMaps, NotInstalledMaps];

            UnknownMaps.PropertyChanged += MapPackItem_PropertyChanged;
            UnknownMaps.IsSelected = settings.MapPacks?.GetValueOrDefault("unknown", true) ?? true;

            NotInstalledMaps.PropertyChanged += MapPackItem_PropertyChanged;
            NotInstalledMaps.IsSelected = settings.MapPacks?.GetValueOrDefault("not_installed", true) ?? true;

            // game modes

            GameModes.AsParallel().ForAll(item => item.PropertyChanged -= GameModeItem_PropertyChanged);
            GameModes.Clear();
            GameModes = [.._resourceSettings.GameTypes
                .Select(gameMode => {
                    SelectableItem<IW4MObjectMap> item = new(gameMode)
                    {
                        Name = gameMode.Alias,
                        IsSelected = settings.GameModes?.GetValueOrDefault(gameMode.Name, true) ?? true
                    };

                    item.PropertyChanged += GameModeItem_PropertyChanged;

                    return item;
                })];

            GameModes.Add(UnknownGameModes);

            UnknownGameModes.PropertyChanged += GameModeItem_PropertyChanged;
            UnknownGameModes.IsSelected = settings.GameModes?.GetValueOrDefault("unknown", true) ?? true;


            // exclude keywords

            ExcludeFilters.Clear();
            foreach (var (keyword, isEnabled) in settings.ExcludeKeywords)
            {
                AddNewExcludeKeyword(keyword, isEnabled);
            }

            OnPropertyChanged(nameof(SelectedMapPacks));
            OnPropertyChanged(nameof(SelectedGameModes));
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

        [RelayCommand(CanExecute = nameof(CanAddNexExcludeKeyword))]
        public void AddNewExcludeKeyword(string keyword)
        {
            AddNewExcludeKeyword(keyword, true);
        }

        private void AddNewExcludeKeyword(string keyword, bool isSelected)
        {
            ExcludeFilters.Add(new(keyword.ToLower(), onRemove: () => RemoveExcludeKeyword(keyword))
            {
                IsSelected = isSelected,
                Name = keyword,
            });
        }

        public bool CanAddNexExcludeKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return !ExcludeFilters.Any(i => i.Model.Equals(keyword, StringComparison.OrdinalIgnoreCase));
        }

        [RelayCommand]
        public void RemoveExcludeKeyword(string keyword)
        {
            SelectableItem<string>? item = ExcludeFilters.FirstOrDefault(i => i.Model.Equals(keyword, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                ExcludeFilters.Remove(item);
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

            if (ExcludeFilters.Where(item => item.IsSelected)
                              .Any(item => server.HostName.Contains(item.Model, StringComparison.OrdinalIgnoreCase)))
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
            SelectableItem<IW4MObjectMap>? gameType = GameModes.FirstOrDefault(gameMode => 
                gameMode.Model.Name.Equals(server.GameType, StringComparison.OrdinalIgnoreCase));

            // if it doesn't exist, assume Unknown
            gameType ??= GameModes.First(gameMode => gameMode.Model.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));

            // is it selected?
            if (!gameType.IsSelected)
            {
                return false;
            }

            if (!server.HasMap)
            {
                return NotInstalledMaps.IsSelected;
            }

            // does the game mode exist?
            SelectableItem<IW4MMapPack>? map = MapPacks.FirstOrDefault(mapPack => 
                mapPack.Model.Maps.Any(m => m.Name.Equals(server.Map, StringComparison.OrdinalIgnoreCase)));

            // if it doesn't exist, assume Unknown
            map ??= UnknownMaps;

            // is it selected?
            if (!map.IsSelected)
            {
                return false;
            }

            return true;
        }

        public ServerFilterSettings ToSettings()
        {
            return new()
            {
                ShowEmpty = ShowEmpty,
                ShowFull = ShowFull,
                ShowPrivate = ShowPrivate,
                MaxPing = MaxPing,
                MinPlayers = MinPlayers,
                MaxPlayers = MaxPlayers,
                MaxSlots = MaxSlots,
                GameModes = GameModes.ToDictionary(item => item.Model.Name, item => item.IsSelected),
                MapPacks = MapPacks.ToDictionary(item => item.Model.Id, item => item.IsSelected),
                ExcludeKeywords = ExcludeFilters.ToDictionary(item => item.Model, item => item.IsSelected)
            };
        }
    }
}
