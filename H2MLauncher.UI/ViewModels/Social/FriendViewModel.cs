﻿using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;
using H2MLauncher.Core.Social.Status;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Messages;

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

        [ObservableProperty]
        private string _test = "[EU] ^1Rasselbande ^7| ^1Vanilla KC/TDM ^7| ^1MW2/MW3 Best Maps";

        /// <summary>
        /// Whether this is the user itself.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Group))]
        [NotifyPropertyChangedFor(nameof(CanAddFriend))]
        [NotifyPropertyChangedFor(nameof(CanInvite))]
        private bool _isSelf;

        /// <summary>
        /// Whether this friend is the party in this users current party.
        /// </summary>
        [ObservableProperty]
        private bool _isPartyLeader;

        /// <summary>
        /// Whether this friend is in THIS USERs current party.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(Group))]
        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [NotifyCanExecuteChangedFor(nameof(KickCommand))]
        [NotifyCanExecuteChangedFor(nameof(PromoteLeaderCommand))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private bool _isInParty;

        /// <summary>
        /// The current id of this friends party.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private string? _partyId;

        /// <summary>
        /// The current size of this friends party.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(IsInThirdParty))]
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private int _partySize;

        /// <summary>
        /// Whether this friend has a party that is open.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private bool _isPartyOpen;

        /// <summary>
        /// Whether this friend has invited the user.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(CanJoinParty))]
        [NotifyCanExecuteChangedFor(nameof(JoinPartyCommand))]
        [ObservableProperty]
        private bool _hasInvited;

        /// <summary>
        /// Whether this friend is currently in another party.
        /// </summary>
        public bool IsInThirdParty => !IsInParty && PartyId is not null;

        /// <summary>
        /// Whether this person is added as a friend.
        /// </summary>
        [NotifyCanExecuteChangedFor(nameof(AddFriendCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveFriendCommand))]
        [NotifyPropertyChangedFor(nameof(CanAddFriend))]
        [NotifyPropertyChangedFor(nameof(CanRemoveFriend))]
        [ObservableProperty]
        private bool _isFriend;

        /// <summary>
        /// The online status of this person.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(Group))]
        [NotifyPropertyChangedFor(nameof(DetailedStatus))]
        [NotifyPropertyChangedFor(nameof(CanInvite))]
        [NotifyCanExecuteChangedFor(nameof(InviteToPartyCommand))]
        [ObservableProperty]
        private OnlineStatus _status;

        /// <summary>
        /// The game status of this person.
        /// </summary>
        [NotifyPropertyChangedFor(nameof(DetailedStatus))]
        [ObservableProperty]
        private GameStatus _gameStatus;

        [NotifyCanExecuteChangedFor(nameof(JoinServerCommand))]
        [NotifyCanExecuteChangedFor(nameof(SelectServerCommand))]
        [NotifyPropertyChangedFor(nameof(DetailedStatus))]
        [NotifyPropertyChangedFor(nameof(HasPlayingServer))]
        [NotifyPropertyChangedFor(nameof(CanJoinServer))]
        [ObservableProperty]
        private PlayingServerViewModel? _playingServer;

        public bool HasPlayingServer => PlayingServer is not null;

        /// <summary>
        /// The group this person is sorted into.
        /// </summary>
        public FriendStatus Group => IsSelf && IsInParty ? FriendStatus.Party : Status switch
        {
            OnlineStatus.Online when IsInParty => FriendStatus.Party,
            OnlineStatus.Online => FriendStatus.Online,
            _ => FriendStatus.Offline
        };

        /// <summary>
        /// The user id.
        /// </summary>
        public string Id { get; init; }

        public bool CanInvite => Status is not OnlineStatus.Offline && !IsSelf;

        public bool CanAddFriend => !IsFriend && !IsSelf;

        public bool CanRemoveFriend => IsFriend;

        public bool CanJoinParty => !IsInParty && (IsPartyOpen || HasInvited);

        public bool CanJoinServer => !IsSelf && HasPlayingServer;

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
                            GameStatus.InMatch => PlayingServer switch
                            {
                                { GameTypeDisplayName: not null, MapDisplayName: not null } =>
                                    $"{PlayingServer.GameTypeDisplayName} on {PlayingServer.MapDisplayName}",
                                { MapDisplayName: not null } => $"In Match on {PlayingServer.MapDisplayName}",
                                _ => "In Match"
                            },
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
        public IAsyncRelayCommand InviteToPartyCommand { get; }

        public IAsyncRelayCommand JoinServerCommand { get; }

        public IRelayCommand SelectServerCommand { get; }

        public IAsyncRelayCommand AddFriendCommand { get; }
        public IAsyncRelayCommand RemoveFriendCommand { get; }

        public IRelayCommand CopyUserIdCommand { get; }
        public IRelayCommand CopyUserNameCommand { get; }

        public FriendViewModel(string userId, PartyClient partyClient, SocialClient socialClient, DialogService dialogService, IServerJoinService serverJoinService)
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

            InviteToPartyCommand = new AsyncRelayCommand(
                async () =>
                {
                    InviteInfo? invite = await partyClient.InviteToParty(Id!);
                    if (invite is not null)
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                               dialogService.OpenTextDialog("Social", $"Sent invite to {Name!}!"));
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                           dialogService.OpenTextDialog("Error", $"Could not send invite to {Name}!"));
                    }
                },
                () => CanInvite && partyClient.IsPartyActive);

            JoinServerCommand = new AsyncRelayCommand(
                () => serverJoinService.JoinServer(PlayingServer!, JoinKind.Normal),
                () => CanJoinServer);

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

            SelectServerCommand = new RelayCommand(() =>
            {
                if (PlayingServer is not null)
                {
                    WeakReferenceMessenger.Default.Send(new SelectServerMessage(PlayingServer));
                }
            }, () => HasPlayingServer);
            CopyUserIdCommand = new RelayCommand(() => Clipboard.SetText(Id));
            CopyUserNameCommand = new RelayCommand(() => Clipboard.SetText(UserName), () => !string.IsNullOrEmpty(UserName));
        }
    }
}
