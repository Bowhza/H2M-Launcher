using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;
using H2MLauncher.UI.Dialog;

using MatchmakingServer.Core.Social;

namespace H2MLauncher.UI.ViewModels
{
    public enum FriendStatus
    {
        Party,
        Online,
        Offline,
    }

    public partial class FriendViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _userName = "";

        [ObservableProperty]
        private bool _showDetails;

        public bool CanAddFriend => !IsFriend && !IsSelf;

        public bool CanRemoveFriend => IsFriend;

        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private bool _canJoinParty;

        [ObservableProperty]
        private bool _canJoinGame;

        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [ObservableProperty]
        private int _partySize;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddFriend))]
        private bool _isSelf;

        [ObservableProperty]
        private bool _isPartyLeader;

        [NotifyPropertyChangedFor(nameof(Group))]
        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [NotifyCanExecuteChangedFor(nameof(KickCommand))]
        [NotifyCanExecuteChangedFor(nameof(PromoteLeaderCommand))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private bool _isInParty;

        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private string? _partyId;

        public bool IsInThirdParty => !IsInParty && PartyId is not null;

        [NotifyCanExecuteChangedFor(nameof(AddFriendCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveFriendCommand))]
        [NotifyPropertyChangedFor(nameof(CanAddFriend))]
        [NotifyPropertyChangedFor(nameof(CanRemoveFriend))]
        [ObservableProperty]
        private bool _isFriend;

        [NotifyPropertyChangedFor(nameof(Group))]
        [NotifyPropertyChangedFor(nameof(DetailedStatus))]
        [ObservableProperty]
        private OnlineStatus _status;

        [NotifyPropertyChangedFor(nameof(DetailedStatus))]
        [ObservableProperty]
        private GameStatus _gameStatus;

        public FriendStatus Group => Status switch
        {
            OnlineStatus.Online when IsInParty => FriendStatus.Party,
            OnlineStatus.Online => FriendStatus.Online,
            _ => FriendStatus.Offline
        };

        public string Id { get; init; }

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
                            _ => "Online"
                        };
                    case OnlineStatus.Offline:
                        return "Offline";
                    default:
                        return "Unknown";
                }
            }
        }

        public IAsyncRelayCommand KickCommand { get; }
        public IAsyncRelayCommand PromoteLeaderCommand { get; }
        public IAsyncRelayCommand JoinPartyCommand { get; }

        public IAsyncRelayCommand AddFriendCommand { get; }
        public IAsyncRelayCommand RemoveFriendCommand { get; }

        public IRelayCommand CopyUserIdCommand { get; }

        public FriendViewModel(string userId, PartyClient partyClient, SocialClient socialClient, DialogService dialogService)
        {
            Id = userId;

            KickCommand = new AsyncRelayCommand(
                () => partyClient.KickMember(Id),
                () => partyClient.IsPartyLeader && !IsSelf);

            PromoteLeaderCommand = new AsyncRelayCommand(
                () => partyClient.PromoteLeader(Id),
                () => partyClient.IsPartyLeader && !IsSelf);

            JoinPartyCommand = new AsyncRelayCommand(
                () => partyClient.JoinParty(PartyId!),
                () => CanJoinParty && PartyId is not null);

            AddFriendCommand = new AsyncRelayCommand(
                async () =>
                {
                    if (await socialClient.AddFriendAsync(Id))
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            dialogService.OpenTextDialog("Social", $"Sent friend request to {Name}!"));
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            dialogService.OpenTextDialog("Error", $"Could not send friend request to {Name}!"));
                    }
                },
                () => CanAddFriend);

            RemoveFriendCommand = new AsyncRelayCommand(
                async () =>
                {
                    bool? result = Application.Current.Dispatcher.Invoke(() =>
                    {
                        return dialogService.OpenTextDialog(
                            title: $"Remove {Name}",
                            text: $"Are you sure you want to remove {Name} from your friends?",
                            acceptButtonText: "Remove");
                    });

                    if (result == true)
                    {
                        if (!await socialClient.RemoveFriendAsync(Id))
                        {
                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                dialogService.OpenTextDialog("Error", $"Could not remove friend {Name}"));
                        }
                    }
                },
                () => CanRemoveFriend);

            CopyUserIdCommand = new RelayCommand(() => Clipboard.SetText(Id));
        }
    }
}
