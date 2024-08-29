using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

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

    ICollectionView ServerCollectionView { get; }
}

public interface IServerTabViewModel<TServerViewModel> : IServerTabViewModel where TServerViewModel : ServerViewModel
{
    new TServerViewModel? SelectedServer { get; }

    new ICollection<TServerViewModel> Servers { get; }

    IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; }

    IRelayCommand<TServerViewModel> JoinServerCommand { get; }
}

public class ServerTabViewModel : ServerTabViewModel<ServerViewModel>
{
    public ServerTabViewModel(string tabName, Action<ServerViewModel> onServerJoin, 
        Predicate<ServerViewModel>? filterPredicate = null) : base(tabName, onServerJoin, filterPredicate)
    {
    }
}

public class RecentServerTabViewModel : ServerTabViewModel<ServerViewModel>
{
    public RecentServerTabViewModel(Action<ServerViewModel> onServerJoin, 
        Predicate<ServerViewModel>? filterPredicate = null) : base("Recents", onServerJoin, filterPredicate)
    {
    }

    protected override SortDescriptionCollection DefaultSorting { get; } = [
        new SortDescription(nameof(ServerViewModel.SortPath), ListSortDirection.Descending),
        new SortDescription(nameof(ServerViewModel.ClientNum), ListSortDirection.Descending),
        new SortDescription(nameof(ServerViewModel.Ping), ListSortDirection.Ascending)
    ];
}

public abstract partial class ServerTabViewModel<TServerViewModel> : ObservableObject, IServerTabViewModel<TServerViewModel>
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

    private readonly CollectionViewSource _collectionViewSource;

    public ObservableCollection<TServerViewModel> Servers { get; } = [];
    ICollection<TServerViewModel> IServerTabViewModel<TServerViewModel>.Servers => Servers;
    IEnumerable<ServerViewModel> IServerTabViewModel.Servers => Servers;

    public ICollectionView ServerCollectionView => _collectionViewSource.View;

    public required IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; set; }

    public IRelayCommand<TServerViewModel> JoinServerCommand { get; set; }

    ServerViewModel? IServerTabViewModel.SelectedServer => SelectedServer;

    protected virtual IEnumerable<SortDescription> DefaultSorting { get; } = [
        new SortDescription(nameof(ServerViewModel.ClientNum), ListSortDirection.Descending),
        new SortDescription(nameof(ServerViewModel.Ping), ListSortDirection.Ascending)
    ];

    public ServerTabViewModel(string tabName, Action<ServerViewModel> onServerJoin, Predicate<TServerViewModel>? filterPredicate = null)
    {
        TabName = tabName;

        // initalize collection view
        _collectionViewSource = new()
        {
            Source = Servers,
        };

        // apply default sorting
        foreach (SortDescription sortDescriptor in DefaultSorting)
        {
            _collectionViewSource.SortDescriptions.Add(sortDescriptor);
        }

        // set filter function
        if (filterPredicate is not null)
        {
            _collectionViewSource.View.Filter = (o) =>
            {
                if (o is not TServerViewModel serverViewModel)
                {
                    return false;
                }

                return filterPredicate(serverViewModel);
            };
        }

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
