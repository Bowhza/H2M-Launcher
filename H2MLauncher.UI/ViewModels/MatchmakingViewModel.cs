using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    internal partial class MatchmakingViewModel : DialogViewModelBase, IDisposable
    {
        private readonly MatchmakingService _matchmakingService;
        private readonly DispatcherTimer _queueTimer;
        private readonly Func<ServerConnectionDetails, Task<bool>> _onForceJoin;

        [ObservableProperty]
        private TimeSpan _queueTime = TimeSpan.Zero;

        public DateTime StartTime { get; init; }

        [NotifyCanExecuteChangedFor(nameof(ForceJoinCommand))]
        [ObservableProperty]
        private bool _isJoining = false;

        [NotifyCanExecuteChangedFor(nameof(EnterMatchmakingCommand))]
        [ObservableProperty]
        private bool _isInQueue = false;

        [ObservableProperty]
        private string _joiningServer = "";

        [ObservableProperty]
        private int _queuePosition = 0;

        [ObservableProperty]
        private int _totalPlayersInQueue = 0;

        [NotifyCanExecuteChangedFor(nameof(EnterMatchmakingCommand))]
        [ObservableProperty]
        private bool _isInMatchmaking = false;

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

        [ObservableProperty]
        private string _title = "";

        [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
        [NotifyCanExecuteChangedFor(nameof(ConnectToServiceCommand))]
        [ObservableProperty]
        private bool _isConnectingToOnlineService = false;

        [ObservableProperty]
        private bool _isError = false;

        [ObservableProperty]
        private string _errorText = "";

        public string QueuePositionText => $"{QueuePosition} / {TotalPlayersInQueue}";

        public IAsyncRelayCommand AbortCommand { get; }

        public IAsyncRelayCommand ForceJoinCommand { get; }

        public IAsyncRelayCommand ConnectToServiceCommand { get; }

        public IAsyncRelayCommand EnterMatchmakingCommand { get; }

        public IAsyncRelayCommand RetryCommand { get; }

        public MatchmakingViewModel(MatchmakingService matchmakingService, Func<ServerConnectionDetails, Task<bool>> onForceJoin)
        {
            _matchmakingService = matchmakingService;
            StartTime = DateTime.Now;

            AbortCommand = new AsyncRelayCommand(Abort);
            ForceJoinCommand = new AsyncRelayCommand(ForceJoin, () => !IsJoining && !string.IsNullOrEmpty(ServerIp) && ServerPort > 0);
            EnterMatchmakingCommand = new AsyncRelayCommand<Playlist?>(EnterMatchmaking, (_) => !IsInMatchmaking && !IsInQueue);
            ConnectToServiceCommand = new AsyncRelayCommand(ConnectToService, () => !IsConnectingToOnlineService && !_matchmakingService.IsConnected);
            RetryCommand = new AsyncRelayCommand(TryAgain, () => !IsConnectingToOnlineService);

            matchmakingService.Joining += MatchmakingService_Joining;
            matchmakingService.JoinFailed += MatchmakingService_JoinFailed;
            matchmakingService.QueuePositionChanged += MatchmakingService_QueuePositionChanged;
            matchmakingService.QueueingStateChanged += MatchmakingService_QueueingStateChanged;
            matchmakingService.MatchFound += MatchmakingService_MatchFound;
            matchmakingService.Matches += MatchmakingService_Matches;
            matchmakingService.MatchSearchCriteriaChanged += MatchmakingService_MatchSearchCriteriaChanged;
            matchmakingService.MatchmakingError += MatchmakingService_MatchmakingError;

            QueuePosition = matchmakingService.QueuePosition;
            TotalPlayersInQueue = matchmakingService.TotalPlayersInQueue;
            IsInMatchmaking = matchmakingService.QueueingState is PlayerState.Matchmaking;
            IsInQueue = matchmakingService.QueueingState is PlayerState.Queued;
            PlaylistName = matchmakingService.Playlist?.Name ?? "";

            _queueTimer = new()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _queueTimer.Tick += QueueTimer_Tick;
            _queueTimer.Start();
            _onForceJoin = onForceJoin;
        }

        partial void OnIsInMatchmakingChanged(bool value)
        {
            if (value)
            {
                Title = "Searching Match";
                MatchmakingStatus = "Searching for matches with ping <= " + _matchmakingService.MatchSearchCriteria?.MaxPing + " ms";
            }
        }

        partial void OnIsInQueueChanged(bool value)
        {
            if (value)
            {
                Title = "Joining Server";
            }
        }

        private void MatchmakingService_MatchmakingError(MatchmakingError reason)
        {
            IsError = true;
            ErrorText = $"Matchmaking error - Reason: {reason}";
        }

        private void MatchmakingService_MatchSearchCriteriaChanged(MatchSearchCriteria matchSearchCriteria)
        {
            MatchmakingStatus = "Searching for matches with ping <= " + matchSearchCriteria.MaxPing + " ms";
        }

        private void MatchmakingService_Matches(IEnumerable<SearchMatchResult> matchResults)
        {
            List<SearchMatchResult> results = matchResults.ToList();
            if (results.Count == 0)
            {
                SearchResultText = $"No matches with >= {_matchmakingService.MatchSearchCriteria?.MinPlayers} players found";
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

        private void MatchmakingService_JoinFailed((string ip, int port) obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsJoining = false;
                JoiningServer = "";
            });
        }

        private async Task Abort()
        {
            if (EnterMatchmakingCommand.IsRunning)
            {
                EnterMatchmakingCommand.Cancel();
            }

            await _matchmakingService.LeaveQueueAsync();
            Application.Current.Dispatcher.Invoke(() => CloseCommand.Execute(null));
        }

        private async Task ForceJoin()
        {
            IsJoining = true;
            JoiningServer = ServerIp + ":" + ServerPort;

            await Task.Yield();

            if (!await _onForceJoin.Invoke((ServerIp, ServerPort)))
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
                Title = "Connecting to online service...";
                try
                {
                    Task delayTask = Task.Delay(1000, cancellationToken);
                    await _matchmakingService.StartConnection(cancellationToken);
                    await delayTask;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    Title = "Not connected";
                }
                catch
                {
                    Title = "Connection Error";
                    IsError = true;
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

        private Playlist? _lastPlaylist = null;

        private async Task EnterMatchmaking(Playlist? playlist, CancellationToken cancellationToken)
        {
            _lastPlaylist = playlist;

            if (!_matchmakingService.IsConnected)
            {
                using var reg = cancellationToken.Register(ConnectToServiceCommand.Cancel);
                await ConnectToServiceCommand.ExecuteAsync(null);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (IsInMatchmaking)
            {
                return;
            }

            bool success = playlist is not null
                ? await _matchmakingService.EnterMatchmakingAsync(playlist)
                : await _matchmakingService.EnterMatchmakingAsync();

            if (!success)
            {
                Title = "Matchmaking Error";
                IsError = true;
                ErrorText = $"Could not enter matchmaking for playlist '{playlist?.Name ?? "Default"}'.";
            }
        }

        private void MatchmakingService_QueueingStateChanged(PlayerState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
             {
                 if (state is PlayerState.Joined or PlayerState.Disconnected)
                 {
                     // we are either joined, disconnected or dequeued for some other reason
                     CloseCommand?.Execute(null);
                 }

                 IsInMatchmaking = state is PlayerState.Matchmaking;
                 IsInQueue = state is PlayerState.Queued;

                 if (IsInMatchmaking)
                 {
                     PlaylistName = _matchmakingService.Playlist?.Name ?? "";
                 }
             });
        }

        private void MatchmakingService_QueuePositionChanged(int position, int totalPlayers)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                QueuePosition = position;
                TotalPlayersInQueue = totalPlayers;
                OnPropertyChanged(nameof(QueuePositionText));
            });
        }

        private void MatchmakingService_Joining((string ip, int port) server)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsJoining = true;
                JoiningServer = $"{server.ip}:{server.port}";
            });
        }

        private void QueueTimer_Tick(object? sender, EventArgs e)
        {
            QueueTime = DateTime.Now - StartTime;
        }

        public void Dispose()
        {
            _matchmakingService.Joining -= MatchmakingService_Joining;
            _matchmakingService.JoinFailed -= MatchmakingService_JoinFailed;
            _matchmakingService.QueuePositionChanged -= MatchmakingService_QueuePositionChanged;
            _matchmakingService.QueueingStateChanged -= MatchmakingService_QueueingStateChanged;
            _matchmakingService.MatchFound -= MatchmakingService_MatchFound;
            _queueTimer.Stop();
            _queueTimer.Tick -= QueueTimer_Tick;
        }
    }
}
