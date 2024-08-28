using System.Collections.ObjectModel;
using System.Collections.Specialized;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.ViewModels;

public interface IServerTabViewModel
{
    string TabName { get; }

    int TotalServers { get; }

    int TotalPlayers { get; }

    ServerViewModel? SelectedServer { get; }

    IEnumerable<ServerViewModel> Servers { get; }
}

public interface IServerTabViewModel<TServerViewModel> : IServerTabViewModel where TServerViewModel : ServerViewModel
{
    new TServerViewModel? SelectedServer { get; }

    new ICollection<TServerViewModel> Servers { get; }

    IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; }

    IRelayCommand<TServerViewModel> JoinServerCommand { get; }
}

public partial class ServerTabViewModel<TServerViewModel> : ObservableObject, IServerTabViewModel<TServerViewModel>
    where TServerViewModel : ServerViewModel
{
    private readonly Action<ServerViewModel> _onServerJoin;

    [ObservableProperty]
    private string _tabName = "";

    [ObservableProperty]
    private int _totalServers = 0;

    [ObservableProperty]
    private int _totalPlayers = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
    private TServerViewModel? _selectedServer;

    public ObservableCollection<TServerViewModel> Servers { get; } = [];
    ICollection<TServerViewModel> IServerTabViewModel<TServerViewModel>.Servers => Servers;
    IEnumerable<ServerViewModel> IServerTabViewModel.Servers => Servers;

    public required IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; init; }

    public IRelayCommand<TServerViewModel> JoinServerCommand { get; init; }

    ServerViewModel? IServerTabViewModel.SelectedServer => SelectedServer;

    public ServerTabViewModel(string tabName, Action<ServerViewModel> onServerJoin)
    {
        TabName = tabName;

        Servers.CollectionChanged += OnServersCollectionChanged;

        _onServerJoin = onServerJoin;

        JoinServerCommand = new RelayCommand<TServerViewModel>((server) =>
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
            TotalPlayers += e.NewItems!.OfType<TServerViewModel>().Sum(s => s.ClientNum);
        }
        else if (e.Action is NotifyCollectionChangedAction.Remove)
        {
            TotalServers -= e.OldItems!.Count;
            TotalPlayers -= e.OldItems!.OfType<TServerViewModel>().Sum(s => s.ClientNum);
        }
        else if (e.Action is NotifyCollectionChangedAction.Reset)
        {
            TotalServers = Servers.Count;
            TotalPlayers = Servers.Sum(s => s.ClientNum);
        }
    }
}
