using System.Collections.ObjectModel;
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.ViewModels;

public sealed partial class ServerTabViewModel : ServerTabViewModel<IServerViewModel>
{
    private ServerTabViewModel() : base(string.Empty, null)
    {
    }
}

public partial class ServerTabViewModel<T> : ObservableObject where T : IServerViewModel
{
    private readonly Action<IServerViewModel> _onServerJoin;

    [ObservableProperty]
    private string _tabName = "";

    [ObservableProperty]
    private int _totalServers = 0;

    [ObservableProperty]
    private int _totalPlayers = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
    private T? _selectedServer;

    public ObservableCollection<T> Servers { get; } = [];

    public required IRelayCommand<IServerViewModel> ToggleFavouriteCommand { get; init; }

    public IRelayCommand<IServerViewModel> JoinServerCommand { get; init; }

    public ServerTabViewModel(string tabName, Action<IServerViewModel> onServerJoin)
    {
        TabName = tabName;

        Servers.CollectionChanged += OnServersCollectionChanged;

        _onServerJoin = onServerJoin;

        JoinServerCommand = new RelayCommand<IServerViewModel>((server) =>
        {
            server ??= SelectedServer;
            if (server is not null)
            {
                _onServerJoin.Invoke(server);
            }

        }, (server) => (server ?? SelectedServer) is not null);
    }

    private void OnServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add)
        {
            TotalServers += e.NewItems!.Count;
            TotalPlayers += e.NewItems!.OfType<T>().Sum(s => s.ClientNum);
        }
        else if (e.Action is NotifyCollectionChangedAction.Remove)
        {
            TotalServers -= e.OldItems!.Count;
            TotalPlayers -= e.OldItems!.OfType<T>().Sum(s => s.ClientNum);
        }
        else if (e.Action is NotifyCollectionChangedAction.Reset)
        {
            TotalServers = Servers.Count;
            TotalPlayers = Servers.Sum(s => s.ClientNum);
        }
    }

    public static implicit operator ServerTabViewModel<T>(ServerTabViewModel<ServerViewModel> v)
    {
        ServerTabViewModel<T> s = new(v.TabName, v._onServerJoin)
        {
            ToggleFavouriteCommand = v.ToggleFavouriteCommand
        };

        return s;
    }

    public static implicit operator ServerTabViewModel<T>(ServerTabViewModel<RecentServerViewModel> v)
    {
        ServerTabViewModel<T> s = new(v.TabName, v._onServerJoin)
        {
            ToggleFavouriteCommand = v.ToggleFavouriteCommand
        };

        return s;
    }
}
