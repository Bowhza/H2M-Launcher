using CommunityToolkit.Mvvm.ComponentModel;

using MatchmakingServer.Core.Social;

namespace H2MLauncher.UI.ViewModels
{
    public partial class FriendViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private bool _showDetails;

        [ObservableProperty]
        private bool _canJoinParty;

        [ObservableProperty]
        private bool _canJoinGame;

        [ObservableProperty]
        private int _partySize;
        
        [ObservableProperty]
        private OnlineStatus _status;

        // New properties for detailed status and party/game info
        private GameStatus _gameStatus;
        public GameStatus GameStatus
        {
            get => _gameStatus;
            set
            {
                if (_gameStatus != value)
                {
                    _gameStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DetailedStatus));
                    OnPropertyChanged(nameof(CanJoinParty));
                    OnPropertyChanged(nameof(CanJoinGame));
                }
            }
        }

        public string Id { get; init; }

        public bool IsInParty => PartySize > 0;

        public bool CanInvite => Status is not OnlineStatus.Offline;
        
        public string DetailedStatus
        {
            get
            {
                switch (Status)
                {
                    case OnlineStatus.Online:
                    case OnlineStatus.InGame:
                        return GameStatus switch
                        {
                            GameStatus.InLobby => "Lobby",
                            GameStatus.InMatch => "In Match",
                            GameStatus.InMainMenu => "Main Menu",
                            _ when Status is OnlineStatus.InGame => "In Game",
                            _ =>  "Online"
                        };
                    case OnlineStatus.Offline:
                        return "Offline";
                    default:
                        return "Unknown";
                }
            }
        }

        public FriendViewModel(string userId)
        {
            Id = userId;
        }
    }
}
