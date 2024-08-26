using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.Core.ViewModels;

public partial class ServerTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _tabName = "";

    [ObservableProperty]
    private int _totalServers = 0;

    [ObservableProperty]
    private int _totalPlayers = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
    private ServerViewModel? _selectedServer;

    public ObservableCollection<ServerViewModel> Servers { get; } = [];

    public required IRelayCommand<ServerViewModel> ToggleFavoriteCommand { get; init; }

    public IRelayCommand<ServerViewModel> JoinServerCommand { get; init; }

    public ServerTabViewModel(string tabName, Action<ServerViewModel> onServerJoin)
    {
        TabName = tabName;

        Servers.CollectionChanged += OnServersCollectionChanged;

        JoinServerCommand = new RelayCommand<ServerViewModel>((server) =>
        {
            server ??= SelectedServer;
            if (server is not null)
            {
                onServerJoin.Invoke(server);
            }

        }, (server) => (server ?? SelectedServer) is not null);
    }

    private void OnServersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            TotalServers += e.NewItems!.Count;
            TotalPlayers += e.NewItems!.OfType<ServerViewModel>().Sum(s => s.ClientNum);
        }
        else if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            TotalServers -= e.OldItems!.Count;
            TotalPlayers -= e.OldItems!.OfType<ServerViewModel>().Sum(s => s.ClientNum);
        }
        else if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            TotalServers = Servers.Count;
            TotalPlayers = Servers.Sum(s => s.ClientNum);
        }
    }
}
