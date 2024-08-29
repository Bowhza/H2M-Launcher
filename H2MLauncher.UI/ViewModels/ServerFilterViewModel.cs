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

            MapPacks.Clear();
            MapPacks = [.._resourceSettings.MapPacks
                .Select(mapPack =>
                {
                    SelectableItem<IW4MMapPack> item = new(mapPack)
                    {
                        Name = mapPack.Name,
                        IsSelected = settings.SelectedMapPacks?.Any(id =>
                            mapPack.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? true
                    };

                    item.PropertyChanged += MapPackItem_PropertyChanged;

                    return item;
                })];

            MapPacks.Add(new SelectableItem<IW4MMapPack>(new IW4MMapPack() { Name = "Unknown", Id = "unknown", Maps = [] })
            {
                Name = "Unknown",
                IsSelected = settings.SelectedMapPacks?.Any(id =>
                    id.Equals("unknown", StringComparison.OrdinalIgnoreCase)) ?? true
            });

            GameModes.Clear();
            GameModes = [.._resourceSettings.GameTypes
                .Select(gameMode => {
                    SelectableItem<IW4MObjectMap> item = new(gameMode)
                    {
                        Name = gameMode.Alias,
                        IsSelected = settings.SelectedGameModes?.Any(name =>
                            gameMode.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? true
                    };

                    item.PropertyChanged += GameModeItem_PropertyChanged;

                    return item;
                })];

            GameModes.Add(new SelectableItem<IW4MObjectMap>(new IW4MObjectMap("Unknown", "Unknown"))
            {
                Name = "Unknown",
                IsSelected = settings.SelectedGameModes?.Any(gameMode =>
                    gameMode.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) ?? true
            });

            foreach (var (keyword, isEnabled) in settings.ExcludeKeywords)
            {
                SelectableItem<string>? existingItem = ExcludeFilters.FirstOrDefault(i => i.Model.Equals(keyword));
                if (existingItem is null)
                {
                    AddNewExcludeKeyword(keyword);
                }
                else
                {
                    existingItem.IsSelected = isEnabled;
                }
            }
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
            ExcludeFilters.Add(new(keyword, onRemove: () => RemoveExcludeKeyword(keyword))
            {
                IsSelected = true,
                Name = keyword,
            });
        }

        public bool CanAddNexExcludeKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return !ExcludeFilters.Any(i => i.Model.Equals(keyword));
        }

        [RelayCommand]
        public void RemoveExcludeKeyword(string keyword)
        {
            SelectableItem<string>? item = ExcludeFilters.FirstOrDefault(i => i.Model.Equals(keyword));
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
                SelectedGameModes = GameModes.Where(item => item.IsSelected).Select(item => item.Model.Name).ToList(),
                SelectedMapPacks = MapPacks.Where(item => item.IsSelected).Select(item => item.Model.Id).ToList(),
                ExcludeKeywords = ExcludeFilters.ToDictionary(item => item.Model, item => item.IsSelected)
            };
        }
    }
}
