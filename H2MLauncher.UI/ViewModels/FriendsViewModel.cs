using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Social;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Services;

using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.ViewModels
{

    public sealed partial class FriendsViewModel : ObservableObject, IDisposable
    {
        private readonly SocialClient _socialClient;
        private readonly PartyClient _partyClient;
        private readonly DialogService _dialogService;
        private readonly IServerJoinService _serverJoinService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IOptions<ResourceSettings> _resourceSettings;
        private readonly DispatcherTimer _timer;

        private readonly HashSet<string> _addedUserIds = [];

        #region Bindings

        [ObservableProperty]
        private bool _isConnecting = false;

        [ObservableProperty]
        private bool _isConnected = false;

        [ObservableProperty]
        private bool _isConnectionError = false;

        [ObservableProperty]
        private bool _isPartyConnected = false;

        public string Status
        {
            get
            {
                if (!_socialClient.IsConnected)
                {
                    return "Disconnected";
                }

                if (_socialClient.IsConnecting)
                {
                    return "Connecting";
                }

                return "Connected";
            }
        }

        public string PartyStatus
        {
            get
            {
                if (!_partyClient.IsConnected)
                {
                    return "Disconnected";
                }

                if (_partyClient.IsConnecting)
                {
                    return "Connecting";
                }

                if (!IsPartyActive)
                {
                    return "Connected";
                }

                if (IsPartyGuest)
                {
                    return $"Joined ({_partyClient.Members?.Count})";
                }

                return _partyClient.Members is not null && _partyClient.Members.Count > 0
                    ? $"{PartyPrivacy} ({_partyClient.Members?.Count})"
                    : PartyPrivacy.ToString();
            }
        }

        public string? UserId => _socialClient.Context.UserId;
        public string? UserName => _socialClient.Context.UserName;

        public bool IsPartyActive => _partyClient.IsPartyActive;
        public bool IsPartyLeader => _partyClient.IsPartyLeader;
        public bool IsPartyGuest => IsPartyActive && !IsPartyLeader;
        public string? PartyId => _partyClient.PartyId;

        public PartyPrivacy PartyPrivacy => _partyClient.PartyPrivacy;

        public bool HasFriends => Friends.Count > 0;

        public ObservableCollection<FriendViewModel> Friends { get; } = [];

        public ICollectionView FriendsGrouped { get; private set; }

        #endregion

        public FriendsViewModel(
            SocialClient socialClient,
            PartyClient partyClient,
            DialogService dialogService,
            IErrorHandlingService errorHandlingService,
            IServerJoinService serverJoinService,
            IOptions<ResourceSettings> resourceSettings)
        {
            _dialogService = dialogService;
            _errorHandlingService = errorHandlingService;
            _serverJoinService = serverJoinService;
            _socialClient = socialClient;
            _socialClient.Context.UserChanged += ClientContext_UserChanged;

            _socialClient.FriendsChanged += SocialClient_FriendsChanged;
            _socialClient.FriendChanged += SocialClient_FriendChanged;
            _socialClient.StatusChanged += SocialClient_StatusChanged;
            _socialClient.ConnectionIssue += SocialClient_ConnectionIssue;
            _socialClient.ConnectionChanged += SocialClient_ConnectionChanged;

            _partyClient = partyClient;
            _partyClient.PartyChanged += PartyService_PartyChanged;
            _partyClient.PartyClosed += PartyService_PartyClosed;
            _partyClient.KickedFromParty += PartyService_KickedFromParty;
            _partyClient.UserChanged += PartyService_UserChanged;
            _partyClient.UserJoined += PartyService_UserJoined;
            _partyClient.UserLeft += PartyService_UserLeft;
            _partyClient.LeaderChanged += PartyClient_LeaderChanged;
            _partyClient.PartyPrivacyChanged += PartyClient_PartyPrivacyChanged;
            _partyClient.InviteReceived += PartyClient_InviteReceived;
            _partyClient.InviteExpired += PartyClient_InviteExpired;
            _partyClient.ConnectionChanged += PartyService_ConnectionChanged;

            FriendsGrouped = CollectionViewSource.GetDefaultView(Friends);
            FriendsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.Group), ListSortDirection.Ascending));

            // Display self always on top
            FriendsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.IsSelf), ListSortDirection.Descending));

            // Then, optionally sort items within each group (e.g., alphabetically by name)
            FriendsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.Name), ListSortDirection.Ascending));


            // Finally, group by the Status property
            FriendsGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FriendViewModel.Group)));

            // Immediately start the connection
            ConnectToHubCommand.Execute(null);

            OnPropertyChanged(nameof(Status));
            _resourceSettings = resourceSettings;

            _timer = new()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            foreach (FriendViewModel friend in Friends)
            {
                friend.PlayingServer?.RecalculatePlayingTime();
            }
        }

        private void ClientContext_UserChanged()
        {
            OnPropertyChanged(nameof(UserId));
            OnPropertyChanged(nameof(UserName));
        }

        private void RefreshPeople()
        {
            Friends.Clear();

            // Keep track of people already added to avoid duplicates
            _addedUserIds.Clear();

            if (_partyClient.IsPartyActive)
            {
                HashSet<string> partyMembersById = new(_partyClient.Members.Select(m => m.Id));

                // Add Party Members not already in Friends list
                foreach (PartyPlayerInfo partyMember in _partyClient.Members)
                {
                    if (_addedUserIds.Add(partyMember.Id))
                    {
                        AddPartyMember(partyMember);
                    }
                }
            }

            if (_socialClient.Friends.Count > 0)
            {
                // Use a HashSet for efficient lookups
                HashSet<string> friendsById = new(_socialClient.Friends.Select(f => f.Id));

                // Add Friends
                foreach (FriendDto friend in _socialClient.Friends)
                {
                    if (_addedUserIds.Add(friend.Id)) // Only add if not already processed (e.g., if also in party list)
                    {
                        AddFriend(friend);
                    }
                }
            }

            OnPropertyChanged(nameof(HasFriends));
            OnPropertyChanged(nameof(Status));
        }

        #region Event handlers

        private void PartyService_ConnectionChanged(bool connected)
        {
            if (!connected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    bool isConnectionLoss = IsPartyConnected;

                    IsPartyConnected = false;
                    RefreshPeople();

                    OnPropertyChanged(nameof(PartyId));
                    OnPropertyChanged(nameof(IsPartyLeader));
                    OnPropertyChanged(nameof(IsPartyActive));
                    OnPropertyChanged(nameof(IsPartyGuest));
                    OnPropertyChanged(nameof(PartyStatus));

                    if (isConnectionLoss)
                    {
                        _dialogService.OpenTextDialog("Party", "Connection to party was lost.");
                    }
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsPartyConnected = true;
                    OnPropertyChanged(nameof(PartyStatus));
                });
            }

        }

        private void PartyService_PartyChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();

                OnPropertyChanged(nameof(PartyId));
                OnPropertyChanged(nameof(IsPartyLeader));
                OnPropertyChanged(nameof(IsPartyActive));
                OnPropertyChanged(nameof(IsPartyGuest));
            });
        }

        private void PartyService_PartyClosed()
        {
            Application.Current.Dispatcher.InvokeAsync(() => _dialogService.OpenTextDialog("Party", "Party was closed!"));
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();

                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyService_KickedFromParty()
        {
            Application.Current.Dispatcher.Invoke(() => _dialogService.OpenTextDialog("Party", "You were kicked from the party!"));
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();

                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyService_UserJoined(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();

                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyService_UserLeft(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();

                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyService_UserChanged(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FriendViewModel? viewModel = Friends.FirstOrDefault(m => m.Id == member.Id);
                if (viewModel is not null)
                {
                    viewModel.Name = member.Name;
                    viewModel.IsPartyLeader = member.IsLeader;
                    viewModel.IsSelf = _partyClient.IsSelf(member);
                }

                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyClient_LeaderChanged(PartyPlayerInfo? oldLeader, PartyPlayerInfo newLeader)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (oldLeader is not null)
                {
                    PartyService_UserChanged(oldLeader);
                }

                PartyService_UserChanged(newLeader);
                OnPropertyChanged(nameof(IsPartyLeader));

                foreach (FriendViewModel viewModel in Friends)
                {
                    viewModel.KickCommand.NotifyCanExecuteChanged();
                    viewModel.PromoteLeaderCommand.NotifyCanExecuteChanged();
                }
            });
        }

        private void PartyClient_PartyPrivacyChanged(PartyPrivacy newPartyPrivacy)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(PartyPrivacy));
                OnPropertyChanged(nameof(PartyStatus));
            });
        }

        private void PartyClient_InviteExpired(string partyId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FriendViewModel? viewModel = Friends.FirstOrDefault(m => m.PartyId == partyId);
                if (viewModel is not null)
                {
                    viewModel.HasInvited = false;
                }
            });
        }

        private void PartyClient_InviteReceived(PartyInvite partyInvite)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FriendViewModel? viewModel = Friends.FirstOrDefault(m => m.Id == partyInvite.SenderId);
                if (viewModel is not null)
                {
                    viewModel.HasInvited = true;
                }
                else
                {
                    // TODO: show somewhere else??
                }
            });
        }

        private void SocialClient_StatusChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FriendViewModel? selfViewModel = Friends.FirstOrDefault(m => m.Id == _socialClient.Context.UserId);
                if (selfViewModel is not null)
                {
                    selfViewModel.Status = _socialClient.OnlineStatus;
                    selfViewModel.GameStatus = _socialClient.GameStatus;
                }
            });
        }

        private void SocialClient_FriendsChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshPeople();
            });
        }

        private void SocialClient_FriendChanged(FriendDto friend)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateFriend(friend);
            });
        }

        private void SocialClient_ConnectionChanged(bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnecting = _socialClient.IsConnecting;
                IsConnected = isConnected;

                // Since we don't yet have a initial retry mechanism, we show the error when disconnected.
                IsConnectionError = !isConnected;

                OnPropertyChanged(nameof(Status));
            });
        }

        private void SocialClient_ConnectionIssue()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnectionError = true;
            });
        }

        #endregion


        [RelayCommand]
        public async Task ConnectToHub()
        {
            Task connectionTask = _socialClient.StartConnection()
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsConnecting = false;
                            IsConnected = false;

                            // Since we don't yet have a initial retry mechanism, we show the error when disconnected.
                            IsConnectionError = true;
                        });
                    }
                });

            if (!connectionTask.IsCompleted)
            {
                IsConnecting = true;
            }

            await connectionTask;
        }

        [RelayCommand]
        public async Task AcceptFriendRequest(string? friendId)
        {
            friendId ??= _dialogService.OpenInputDialog(
                title: "Accept Friend Request",
                text: "Enter or paste the ID of the friend to acceot:",
                acceptButtonText: "Accept");

            if (friendId is null)
            {
                return;
            }

            if (!Guid.TryParse(friendId, out _))
            {
                _errorHandlingService.HandleError("Invalid friend id.");
                return;
            }

            if (!await _socialClient.AcceptFriendAsync(friendId))
            {
                _errorHandlingService.HandleError("Could not accept friend request.");
            }
        }

        [RelayCommand]
        public async Task CreateParty()
        {
            if (_partyClient.IsPartyActive)
            {
                return;
            }

            if (await _partyClient.CreateParty() is null)
            {
                _errorHandlingService.HandleError("Could not create party.");
            }
        }

        [RelayCommand]
        public async Task JoinParty(string? partyId)
        {
            partyId ??= _dialogService.OpenInputDialog("Join Party", "Enter or paste the ID of the party to join:",
                acceptButtonText: "Join");

            if (partyId is null)
            {
                return;
            }

            if (!Guid.TryParse(partyId, out _))
            {
                _errorHandlingService.HandleError("Invalid party id.");
                return;
            }

            if (!await _partyClient.JoinParty(partyId))
            {
                _errorHandlingService.HandleError("Could not join party.");
            }
        }

        [RelayCommand]
        public void CopyPartyId()
        {
            if (PartyId is null) return;

            Clipboard.SetText(PartyId);
        }

        [RelayCommand]
        public Task LeaveParty()
        {
            return _partyClient.LeaveParty();
        }

        [RelayCommand]
        public Task ChangePartyPrivacy()
        {
            PartyPrivacy[] enumValues = Enum.GetValues<PartyPrivacy>();
            int currentIndex = Array.IndexOf(enumValues, PartyPrivacy);
            int nextIndex = (currentIndex + 1) % enumValues.Length;

            return _partyClient.ChangePrivacy(enumValues[nextIndex]);
        }

        private void AddFriend(FriendDto friend)
        {
            PartyPlayerInfo? partyPlayer = _partyClient.Members?.FirstOrDefault(m => m.Id == friend.Id);
            FriendViewModel friendViewModel = new(friend.Id, _partyClient, _socialClient, _dialogService, _serverJoinService)
            {
                Name = friend.PlayerName ?? friend.UserName,
                UserName = friend.UserName,
                Status = friend.Status,
                GameStatus = friend.GameStatus,
                IsFriend = true,
                IsInParty = partyPlayer is not null,
                IsPartyLeader = partyPlayer?.IsLeader ?? false,
                IsSelf = partyPlayer is not null && _partyClient.IsSelf(partyPlayer),
                PartySize = friend.PartyStatus?.Size ?? 0,
                PartyId = friend.PartyStatus?.PartyId,
                IsPartyOpen = friend.PartyStatus?.IsOpen == true,
                HasInvited = UserId is not null &&
                             friend.PartyStatus is not null &&
                             friend.PartyStatus.Invites.Contains(UserId),
                PlayingServer = CreatePlayingServerViewModel(friend.MatchStatus),
            };

            Friends.Add(friendViewModel);
        }

        private void AddPartyMember(PartyPlayerInfo member)
        {
            bool isSelf = _partyClient.IsSelf(member);
            FriendDto? friend = _socialClient.Friends?.FirstOrDefault(f => f.Id == member.Id);
            FriendViewModel friendViewModel = new(member.Id, _partyClient, _socialClient, _dialogService, _serverJoinService)
            {
                Name = member.Name,
                UserName = friend?.UserName ?? (isSelf ? _socialClient.Context.UserName ?? "" : member.UserName),
                Status = friend?.Status ?? OnlineStatus.Online,
                GameStatus = friend?.GameStatus ?? (isSelf ? _socialClient.GameStatus : GameStatus.None),
                IsFriend = friend is not null,
                IsInParty = true,
                IsPartyLeader = member.IsLeader,
                IsSelf = isSelf,
                PartySize = _partyClient.Members?.Count ?? 0,
                PartyId = friend?.PartyStatus?.PartyId,
            };

            Friends.Add(friendViewModel);
        }

        private PlayingServerViewModel? CreatePlayingServerViewModel(MatchStatusDto? matchStatus)
        {
            if (matchStatus is null)
            {
                return null;
            }

            return new()
            {
                Ip = matchStatus.Server.Ip,
                Port = matchStatus.Server.Port,
                ServerName = matchStatus.ServerName,
                MapDisplayName = _resourceSettings.Value.GetMapDisplayName(matchStatus.MapName ?? ""),
                GameTypeDisplayName = _resourceSettings.Value.GetGameTypeDisplayName(matchStatus.GameMode ?? ""),
                JoinedAt = matchStatus.JoinedAt,
            };
        }

        private void UpdateFriend(FriendDto friend)
        {
            FriendViewModel? friendViewModel = Friends.FirstOrDefault(m => m.Id == friend.Id);
            if (friendViewModel is not null)
            {
                friendViewModel.Name = friend.PlayerName ?? friend.UserName;
                friendViewModel.UserName = friend.UserName;
                friendViewModel.Status = friend.Status;
                friendViewModel.GameStatus = friend.GameStatus;
                friendViewModel.IsFriend = true;
                friendViewModel.PartySize = friend.PartyStatus?.Size ?? 0;
                friendViewModel.PartyId = friend.PartyStatus?.PartyId;
                friendViewModel.IsPartyOpen = friend.PartyStatus?.IsOpen == true;
                friendViewModel.HasInvited = UserId is not null &&
                                             friend.PartyStatus is not null &&
                                             friend.PartyStatus.Invites.Contains(UserId);
                friendViewModel.PlayingServer = CreatePlayingServerViewModel(friend.MatchStatus);
            }
        }

        public void Dispose()
        {
            _socialClient.Context.UserChanged -= ClientContext_UserChanged;
            _socialClient.FriendsChanged -= SocialClient_FriendsChanged;
            _socialClient.FriendChanged -= SocialClient_FriendChanged;

            _partyClient.PartyChanged -= PartyService_PartyChanged;
            _partyClient.PartyClosed -= PartyService_PartyClosed;
            _partyClient.KickedFromParty -= PartyService_KickedFromParty;
            _partyClient.UserChanged -= PartyService_UserChanged;
            _partyClient.UserJoined -= PartyService_UserJoined;
            _partyClient.UserLeft -= PartyService_UserLeft;
            _partyClient.LeaderChanged -= PartyClient_LeaderChanged;
            _partyClient.ConnectionChanged -= PartyService_ConnectionChanged;

            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }

        public class FriendStatusGroupComparer : IComparer
        {
            private static readonly Dictionary<FriendStatus, int> StatusOrder = new()
            {
                { FriendStatus.Party, 0 },
                { FriendStatus.Online, 1 },
                { FriendStatus.Offline, 2 }
            };

            public int Compare(object? x, object? y)
            {
                if (x is FriendViewModel groupX && y is FriendViewModel groupY)
                {
                    int orderX, orderY = int.MaxValue;

                    StatusOrder.TryGetValue(groupX.Group, out orderX);
                    StatusOrder.TryGetValue(groupY.Group, out orderY);

                    return orderX.CompareTo(orderY);
                }

                return 0;
            }
        }
    }
}
