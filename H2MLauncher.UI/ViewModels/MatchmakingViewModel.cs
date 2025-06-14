using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public partial class MatchmakingViewModel : DialogViewModelBase, IDisposable
    {
        private readonly MatchmakingService _matchmakingService;
        private readonly QueueingService _queueingService;
        private readonly IOnlineServices _onlineService;


        private readonly CachedServerDataService _serverDataService;
        private readonly IServerJoinService _serverJoinService;
        private readonly DispatcherTimer _queueTimer;

        [ObservableProperty]
        private TimeSpan _queueTime = TimeSpan.Zero;

        public DateTime StartTime { get; set; }

        [NotifyCanExecuteChangedFor(nameof(ForceJoinCommand))]
        [NotifyCanExecuteChangedFor(nameof(EnterMatchmakingCommand))]
        [NotifyPropertyChangedFor(nameof(CanEnterMatchmaking))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [ObservableProperty]
        private bool _isJoining = false;

        [ObservableProperty]
        private string _joiningServer = "";

        [ObservableProperty]
        private int _queuePosition = 0;

        [ObservableProperty]
        private int _totalPlayersInQueue = 0;

        [NotifyCanExecuteChangedFor(nameof(ForceJoinCommand))]
        [ObservableProperty]
        private string _serverIp = "";

        [NotifyCanExecuteChangedFor(nameof(ForceJoinCommand))]
        [ObservableProperty]
        private int _serverPort;

        [ObservableProperty]
        private string _serverHostName = "";

        [ObservableProperty]
        private string _playlistName = "";

        [ObservableProperty]
        private string _matchmakingStatus = "";

        [ObservableProperty]
        private string _searchResultText = "";

        [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
        [NotifyCanExecuteChangedFor(nameof(ConnectToServiceCommand))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [ObservableProperty]
        private bool _isConnectingToOnlineService = false;

        [NotifyCanExecuteChangedFor(nameof(EnterMatchmakingCommand))]
        [NotifyPropertyChangedFor(nameof(CanEnterMatchmaking))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [ObservableProperty]
        private bool _isConnectedToOnlineService = false;

        [NotifyPropertyChangedFor(nameof(Title))]
        [ObservableProperty]
        private bool _isError = false;

        [ObservableProperty]
        private string? _errorTitle;

        [ObservableProperty]
        private string _errorText = "";

        [ObservableProperty]
        private MatchmakingPreferencesViewModel _matchmakingPreferences = new();

        private Playlist? _lastPlaylist = null;

        [ObservableProperty]
        private Playlist? _selectedPlaylist = null;

        [NotifyCanExecuteChangedFor(nameof(EnterMatchmakingCommand))]
        [NotifyPropertyChangedFor(nameof(CanEnterMatchmaking))]
        [NotifyPropertyChangedFor(nameof(IsInMatchmaking))]
        [NotifyPropertyChangedFor(nameof(IsInQueue))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [ObservableProperty]
        private PlayerState _state;
        public bool IsInMatchmaking => State is PlayerState.Matchmaking;
        public bool IsInQueue => State is PlayerState.Queued;

        public bool CanEnterMatchmaking => IsConnectedToOnlineService &&
            State is PlayerState.Connected or PlayerState.Joined && !IsJoining;

        public string QueuePositionText => $"{QueuePosition} / {TotalPlayersInQueue}";

        public ObservableCollection<Playlist> Playlists { get; } = [
            new Playlist()
            {
                Id = "Default",
                Name = "Default Playlist"
            }];


        public bool CloseOnLeave { get; set; } = false;

        public string Title
        {
            get
            {
                if (IsError && !string.IsNullOrEmpty(ErrorTitle))
                {
                    return ErrorTitle;
                }
                if (State is PlayerState.Matchmaking)
                {
                    return "Searching Match";
                }
                if (State is PlayerState.Queued or PlayerState.Joining)
                {
                    return "Joining Server";
                }
                if (IsConnectingToOnlineService)
                {
                    return "Connecting to online service...";
                }
                if (IsConnectedToOnlineService)
                {
                    return "Matchmaking";
                }

                return "Matchmaking";
            }
        }

        public IAsyncRelayCommand AbortCommand { get; }

        public IAsyncRelayCommand ForceJoinCommand { get; }

        public IAsyncRelayCommand ConnectToServiceCommand { get; }

        public IAsyncRelayCommand EnterMatchmakingCommand { get; }

        public IAsyncRelayCommand LeaveQueueCommand { get; }

        public IAsyncRelayCommand RetryCommand { get; }

        public MatchmakingViewModel(
            MatchmakingService matchmakingService,
            QueueingService queueingService,
            IOnlineServices onlineService,
            CachedServerDataService serverDataService,
            IServerJoinService serverJoinService)
        {
            _matchmakingService = matchmakingService;
            _queueingService = queueingService;
            _onlineService = onlineService;

            _serverDataService = serverDataService;
            _serverJoinService = serverJoinService;

            AbortCommand = new AsyncRelayCommand(Abort);
            ForceJoinCommand = new AsyncRelayCommand(ForceJoin, () => !IsJoining && !string.IsNullOrEmpty(ServerIp) && ServerPort > 0);
            EnterMatchmakingCommand = new AsyncRelayCommand<Playlist?>(EnterMatchmaking, (_) => CanEnterMatchmaking);
            ConnectToServiceCommand = new AsyncRelayCommand(ConnectToService, () => !IsConnectingToOnlineService && !IsConnectedToOnlineService);
            RetryCommand = new AsyncRelayCommand(TryAgain, () => !IsConnectingToOnlineService);
            LeaveQueueCommand = new AsyncRelayCommand(LeaveQueue);

            queueingService.Joining += QueueingService_Joining;
            queueingService.JoinFailed += QueueingService_JoinFailed;
            queueingService.QueuePositionChanged += QueueingService_QueuePositionChanged;
            onlineService.StateChanged += OnlineService_StateChanged;
            matchmakingService.MatchFound += MatchmakingService_MatchFound;
            matchmakingService.Matches += MatchmakingService_Matches;
            matchmakingService.MatchSearchCriteriaChanged += MatchmakingService_MatchSearchCriteriaChanged;
            matchmakingService.RemovedFromMatchmaking += MatchmakingService_RemovedFromMatchmaking;

            QueuePosition = queueingService.QueuePosition;
            TotalPlayersInQueue = queueingService.TotalPlayersInQueue;
            State = onlineService.State;
            PlaylistName = matchmakingService.Playlist?.Name ?? "";
            IsConnectingToOnlineService = matchmakingService.IsConnecting;
            IsConnectedToOnlineService = matchmakingService.IsConnected;
            SelectedPlaylist = Playlists.FirstOrDefault();

            if (queueingService.QueuedServer is not null)
            {
                ServerIp = queueingService.QueuedServer.Ip;
                ServerPort = queueingService.QueuedServer.Port;
                ServerHostName = string.IsNullOrEmpty(queueingService.QueuedServer.ServerName)
                    ? $"{ServerIp}:{ServerPort}"
                    : queueingService.QueuedServer.ServerName;
            }

            _queueTimer = new()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _queueTimer.Tick += QueueTimer_Tick;

            if (State is PlayerState.Queued or PlayerState.Matchmaking)
            {
                // start counting seconds from 0
                StartTime = DateTime.Now;
                _queueTimer.Start();
            }
        }

        partial void OnIsErrorChanged(bool oldValue, bool newValue)
        {
            if (!newValue)
            {
                ErrorTitle = null;
            }
        }

        protected override async Task OnLoaded()
        {
            if (ConnectToServiceCommand.CanExecute(null))
            {
                ConnectToServiceCommand.Execute(null);
            }

            if (State is not PlayerState.Queued)
            {
                await RefreshPlaylists();
            }
        }

        private async Task RefreshPlaylists()
        {
            try
            {
                IReadOnlyList<Playlist>? playlists = await _serverDataService.GetPlaylists(CancellationToken.None);
                if (playlists is null)
                {
                    return;
                }

                Playlists.Clear();
                foreach (Playlist playlist in playlists)
                {
                    Playlists.Add(playlist);
                }
                SelectedPlaylist = Playlists.FirstOrDefault();
            }
            catch
            {
                IsError = true;
                ErrorText = "Failed to fetch the playlists. Please try again later.";
                ErrorTitle = "Error";
            }
        }

        private async Task Abort()
        {
            if (ConnectToServiceCommand.IsRunning)
            {
                ConnectToServiceCommand.Cancel();
                Application.Current.Dispatcher.Invoke(() => CloseCommand.Execute(null));
                return;
            }

            if (EnterMatchmakingCommand.IsRunning)
            {
                EnterMatchmakingCommand.Cancel();
            }

            await _matchmakingService.LeaveQueueAsync();
            Application.Current.Dispatcher.Invoke(() => CloseCommand.Execute(null));
        }

        private async Task LeaveQueue()
        {
            await _matchmakingService.LeaveQueueAsync();
            if (CloseOnLeave)
            {
                Application.Current.Dispatcher.Invoke(() => CloseCommand.Execute(null));
            }
        }

        private async Task ForceJoin()
        {
            if (_queueingService.QueuedServer is null)
            {
                return;
            }

            IsJoining = true;
            JoiningServer = ServerIp + ":" + ServerPort;

            JoinServerResult joinResult = await _serverJoinService.JoinServerDirectly(
                server: _queueingService.QueuedServer,
                password: _queueingService.QueuedServer.Password,
                kind: JoinKind.Forced);

            if (joinResult is not JoinServerResult.Success)
            {
                // not successful
                IsJoining = false;
                JoiningServer = "";
            }
        }

        private Task TryAgain()
        {
            if (!_matchmakingService.IsConnected)
            {
                return ConnectToServiceCommand.ExecuteAsync(null);
            }

            return EnterMatchmakingCommand.ExecuteAsync(_lastPlaylist);
        }

        private async Task<bool> ConnectToService(CancellationToken cancellationToken)
        {
            if (!_matchmakingService.IsConnected)
            {
                IsConnectingToOnlineService = true;
                IsError = false;
                try
                {
                    Task delayTask = Task.Delay(1000, cancellationToken);
                    await _matchmakingService.StartConnection(cancellationToken);
                    await delayTask;
                    IsConnectedToOnlineService = true;
                    return true;
                }
                catch (OperationCanceledException) { }
                catch
                {
                    IsError = true;
                    ErrorTitle = "Connection Error";
                    ErrorText = "The matchmaking service is currently not available.";
                    return false;
                }
                finally
                {
                    IsConnectingToOnlineService = false;
                }
            }

            return true;
        }

        private async Task EnterMatchmaking(Playlist? playlist, CancellationToken cancellationToken)
        {
            _lastPlaylist = playlist ??= SelectedPlaylist;

            if (!_matchmakingService.IsConnected)
            {
                using var reg = cancellationToken.Register(ConnectToServiceCommand.Cancel);
                await ConnectToServiceCommand.ExecuteAsync(null);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            bool success = playlist is not null
                ? await _matchmakingService.EnterMatchmakingAsync(playlist, MatchmakingPreferences.ToModel())
                : await _matchmakingService.EnterMatchmakingAsync(MatchmakingPreferences.ToModel());

            if (!success)
            {
                IsError = true;
                ErrorTitle = "Matchmaking Error";
                ErrorText = $"Could not enter matchmaking for playlist '{playlist?.Name ?? "Default"}'.";
            }
        }

        private void OnlineService_StateChanged(PlayerState oldState, PlayerState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (state is PlayerState.Joined or PlayerState.Disconnected)
                {
                    // we are either joined, disconnected or dequeued for some other reason
                    CloseCommand?.Execute(null);
                }

                if ((CloseOnLeave || !_matchmakingService.IsActiveSearcher) &&
                    state is PlayerState.Connected &&
                    oldState is PlayerState.Matchmaking or PlayerState.Queued)
                {
                    // we were in matchmaking or queue and left
                    CloseCommand?.Execute(null);
                }

                State = state;

                if (state is PlayerState.Matchmaking)
                {
                    PlaylistName = _matchmakingService.Playlist?.Name ?? "";
                }
                else
                {
                    MatchmakingStatus = "";
                    SearchResultText = "";
                }

                if (state is PlayerState.Matchmaking or PlayerState.Queued && !_queueTimer.IsEnabled)
                {
                    // start counting seconds from 0
                    StartTime = DateTime.Now;
                    _queueTimer.Start();

                    // probably queued from the server, so add server info
                    if (_queueingService.QueuedServer is not null)
                    {
                        ServerIp = _queueingService.QueuedServer.Ip;
                        ServerPort = _queueingService.QueuedServer.Port;
                        ServerHostName = string.IsNullOrEmpty(_queueingService.QueuedServer.ServerName)
                            ? $"{ServerIp}:{ServerPort}"
                            : _queueingService.QueuedServer.ServerName;
                    }
                }

                if (state is PlayerState.Connected && _queueTimer.IsEnabled)
                {
                    _queueTimer.Stop();
                }

                IsJoining = state is PlayerState.Joining;
            });
        }

        private void MatchmakingService_RemovedFromMatchmaking(MatchmakingError reason)
        {
            if (reason is not MatchmakingError.UserLeave)
            {
                IsError = true;
                ErrorText = $"Matchmaking error - Reason: {reason}";
            }
        }

        private void MatchmakingService_MatchSearchCriteriaChanged(MatchSearchCriteria matchSearchCriteria)
        {
            MatchmakingStatus = "Searching for matches with ping <= " + (matchSearchCriteria.MaxPing) + " ms";
        }

        private void MatchmakingService_Matches(IEnumerable<SearchMatchResult> matchResults)
        {
            List<SearchMatchResult> results = matchResults.ToList();
            if (results.Count == 0)
            {
                SearchResultText = $"No matches with {_matchmakingService.MatchSearchCriteria?.MinPlayers} players found";
            }
            else
            {
                SearchMatchResult bestMatch = results.OrderByDescending(r => r.MatchQuality).First();
                SearchResultText = $"{results.Count} matches found (best with {bestMatch.NumPlayers} players)";
            }
        }

        private void MatchmakingService_MatchFound((string hostname, SearchMatchResult match) obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServerHostName = obj.hostname;
                ServerIp = obj.match.ServerIp;
                ServerPort = obj.match.ServerPort;
            });
        }

        private void QueueingService_Joining(IServerConnectionDetails server)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsJoining = true;
                JoiningServer = $"{server.Ip}:{server.Port}";
            });
        }

        private void QueueingService_JoinFailed(IServerConnectionDetails obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsJoining = false;
                JoiningServer = "";
            });
        }

        private void QueueingService_QueuePositionChanged(int position, int totalPlayers)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                QueuePosition = position;
                TotalPlayersInQueue = totalPlayers;
                OnPropertyChanged(nameof(QueuePositionText));
            });
        }

        private void QueueTimer_Tick(object? sender, EventArgs e)
        {
            QueueTime = DateTime.Now - StartTime;
        }

        public void Dispose()
        {
            _queueingService.Joining -= QueueingService_Joining;
            _queueingService.JoinFailed -= QueueingService_JoinFailed;
            _queueingService.QueuePositionChanged -= QueueingService_QueuePositionChanged;
            _onlineService.StateChanged -= OnlineService_StateChanged;
            _matchmakingService.MatchFound -= MatchmakingService_MatchFound;
            _matchmakingService.Matches -= MatchmakingService_Matches;
            _matchmakingService.MatchSearchCriteriaChanged -= MatchmakingService_MatchSearchCriteriaChanged;
            _matchmakingService.RemovedFromMatchmaking -= MatchmakingService_RemovedFromMatchmaking;
            _queueTimer.Stop();
            _queueTimer.Tick -= QueueTimer_Tick;
        }
    }
}
