using System.Windows;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    internal partial class QueueViewModel : DialogViewModelBase, IDisposable
    {
        private readonly MatchmakingService _matchmakingService;
        private readonly DispatcherTimer _queueTimer;

        [ObservableProperty]
        private ServerViewModel _server;

        [ObservableProperty]
        private TimeSpan _queueTime = TimeSpan.Zero;

        public DateTime StartTime { get; init; }

        [ObservableProperty]
        private bool _isJoining = false;

        [ObservableProperty]
        private string _joiningServer = "";

        [ObservableProperty]
        private int _queuePosition = 0;

        [ObservableProperty]
        private int _totalPlayersInQueue = 0;

        public string QueuePositionText => $"{QueuePosition} / {TotalPlayersInQueue}";

        public IAsyncRelayCommand LeaveQueueCommand { get; }

        public IRelayCommand ForceJoinCommand { get; }

        public QueueViewModel(ServerViewModel server, MatchmakingService matchmakingService)
        {
            _matchmakingService = matchmakingService;
            Server = server;
            StartTime = DateTime.Now;

            LeaveQueueCommand = new AsyncRelayCommand(() => matchmakingService.LeaveQueueAsync()
                .ContinueWith((_) => CloseCommand.Execute(null), TaskScheduler.FromCurrentSynchronizationContext()));
            ForceJoinCommand = new RelayCommand(() => CloseCommand.Execute(true), () => CloseCommand.CanExecute(true));

            matchmakingService.Joining += MatchmakingService_Joining;
            matchmakingService.QueuePositionChanged += MatchmakingService_QueuePositionChanged;
            matchmakingService.QueueingStateChanged += MatchmakingService_QueueingStateChanged;

            QueuePosition = matchmakingService.QueuePosition;
            TotalPlayersInQueue = matchmakingService.TotalPlayersInQueue;

            _queueTimer = new()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _queueTimer.Tick += QueueTimer_Tick;
            _queueTimer.Start();
        }

        private void MatchmakingService_QueueingStateChanged(PlayerState state)
        {
           Application.Current.Dispatcher.Invoke(() =>
            {
                if (state is not PlayerState.Joining and not PlayerState.Queued)
                {
                    // we are either joined, disconnected or dequeued for some other reason
                    CloseCommand?.Execute(null);
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
            IsJoining = true;
            JoiningServer = $"{server.ip}:{server.port}";
        }

        private void QueueTimer_Tick(object? sender, EventArgs e)
        {
            QueueTime = DateTime.Now - StartTime;
        }

        public void Dispose()
        {
            _matchmakingService.Joining -= MatchmakingService_Joining;
            _matchmakingService.QueuePositionChanged -= MatchmakingService_QueuePositionChanged;
            _matchmakingService.QueueingStateChanged -= MatchmakingService_QueueingStateChanged;
            _queueTimer.Stop();
            _queueTimer.Tick -= QueueTimer_Tick;
        }
    }
}
