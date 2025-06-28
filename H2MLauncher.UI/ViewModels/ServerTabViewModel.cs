using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Utilities;

using Microsoft.IdentityModel.Tokens;

namespace H2MLauncher.UI.ViewModels;

public interface IServerTabViewModel
{
    ServerBrowserViewModel Parent { get; }

    string TabName { get; }

    int TotalServers { get; }

    int TotalPlayers { get; }

    ServerViewModel? SelectedServer { get; }

    IEnumerable<ServerViewModel> Servers { get; }

    ICollectionView ServerCollectionView { get; }
}

public interface IServerTabViewModel<TServerViewModel> : IServerTabViewModel where TServerViewModel : ServerViewModel
{
    new TServerViewModel? SelectedServer { get; set; }

    new ICollection<TServerViewModel> Servers { get; }

    IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; }

    IAsyncRelayCommand<TServerViewModel> JoinServerCommand { get; }
}

public class ServerTabViewModel : ServerTabViewModel<ServerViewModel>
{
    public ServerTabViewModel(string tabName, ServerBrowserViewModel parent, Func<ServerViewModel, Task> onServerJoin,
        Predicate<ServerViewModel>? filterPredicate = null) : base(tabName, parent, onServerJoin, filterPredicate)
    {
    }
}

public class SelectablePlaylist(CustomPlaylistInfo customPlaylist) : SelectableItem<CustomPlaylistInfo>(customPlaylist);

public partial class CustomServerTabViewModel : ServerTabViewModel<ServerViewModel>
{
    [ObservableProperty]
    private CustomPlaylistInfo _playlist;

    public required IRelayCommand<ServerViewModel> RemoveServerCommand { get; set; }
    public required IRelayCommand<ServerViewModel> AddServerCommand { get; set; }

    public required IRelayCommand EditCommand { get; set; }

    public required IRelayCommand RemoveCommand { get; set; }

    public override IEnumerable<SelectablePlaylist> SelectablePlaylists => 
        base.SelectablePlaylists.Select(item =>
        {
            if (item.Model.Id == Playlist.Id)
            {
                item.IsSelectable = false;
            }

            return item;
        });

    public CustomServerTabViewModel(
            CustomPlaylistInfo playlist,
            ServerBrowserViewModel parent,
            Func<ServerViewModel, Task> onServerJoin,
            Predicate<ServerViewModel>? filterPredicate = null) : base(playlist.Name, parent, onServerJoin, filterPredicate)
    {
        Playlist = playlist;
    }
}

public class RecentServerTabViewModel : ServerTabViewModel<ServerViewModel>
{
    public RecentServerTabViewModel(ServerBrowserViewModel parent, Func<ServerViewModel, Task> onServerJoin,
        Predicate<ServerViewModel>? filterPredicate = null) : base("Recents", parent, onServerJoin, filterPredicate)
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
    private readonly Func<ServerViewModel, Task> _onServerJoin;

    [ObservableProperty]
    private string _tabName = "";

    [ObservableProperty]
    private int _totalServers = 0;

    [ObservableProperty]
    private int _totalPlayers = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
    [NotifyPropertyChangedFor(nameof(SelectablePlaylists))]
    private TServerViewModel? _selectedServer;

    private readonly CollectionViewSource _collectionViewSource;

    public ObservableCollection<TServerViewModel> Servers { get; } = [];
    ICollection<TServerViewModel> IServerTabViewModel<TServerViewModel>.Servers => Servers;
    IEnumerable<ServerViewModel> IServerTabViewModel.Servers => Servers;

    public ICollectionView ServerCollectionView => _collectionViewSource.View;

    public required IRelayCommand<TServerViewModel> ToggleFavouriteCommand { get; set; }

    public IAsyncRelayCommand<TServerViewModel> JoinServerCommand { get; set; }

    public required IRelayCommand<TServerViewModel> AddToNewPlaylistCommand { get; init; }

    public ServerBrowserViewModel Parent { get; }

    public bool IsSelected => Parent.SelectedTab == this;

    /// <summary>
    /// Gets a computed collection of playlists selectable for the currently selected server.
    /// </summary>
    public virtual IEnumerable<SelectablePlaylist> SelectablePlaylists
    {
        get
        {
            if (SelectedServer is null)
            {
                return [];
            }

            return Parent.CustomPlaylists.Select(p =>
            {
                SelectablePlaylist item = new(p)
                {
                    Name = p.Name,
                    IsSelected = p.Servers.Contains(SelectedServer.ToServerConnectionDetails())
                };

                item.PropertyChanged += PlaylistItem_PropertyChanged;

                return item;
            });
        }
    }

    private void PlaylistItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectablePlaylist.IsSelected))
        {
            if (sender is not SelectablePlaylist item)
            {
                return;
            }

            if (item.IsSelected)
            {
                Parent.AddServerToPlaylist(item.Model.Id, SelectedServer);
            }
            else
            {
                Parent.RemoveServerFromPlaylist(item.Model.Id, SelectedServer);
            }
        }
    }

    ServerViewModel? IServerTabViewModel.SelectedServer => SelectedServer;

    protected virtual IEnumerable<SortDescription> DefaultSorting { get; } = [
        new SortDescription(nameof(ServerViewModel.ClientNum), ListSortDirection.Descending),
        new SortDescription(nameof(ServerViewModel.Ping), ListSortDirection.Ascending)
    ];

    public ServerTabViewModel(
        string tabName, 
        ServerBrowserViewModel parent, 
        Func<ServerViewModel, Task> onServerJoin, 
        Predicate<TServerViewModel>? filterPredicate = null)
    {
        TabName = tabName;
        Parent = parent;

        // initialize collection view
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
        Parent.PropertyChanged += Parent_PropertyChanged;

        _onServerJoin = onServerJoin;

        JoinServerCommand = new AsyncRelayCommand<TServerViewModel>(async (server) =>
        {
            server ??= SelectedServer;
            if (server is not null)
            {
                await _onServerJoin.Invoke(server);
            }

        }, (server) => (server ?? SelectedServer) is not null);
    }

    private void Parent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerBrowserViewModel.SelectedTab))
        {
            OnPropertyChanged(nameof(IsSelected));
        }
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
