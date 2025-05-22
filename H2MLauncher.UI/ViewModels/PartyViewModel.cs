using System.Collections.ObjectModel;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public partial class PartyMemberViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private bool _isLeader;

        [ObservableProperty]
        private bool _isSelf;

        public string Id { get; init; }

        public IAsyncRelayCommand KickCommand { get; }
        public IAsyncRelayCommand PromoteLeaderCommand { get; }

        public PartyMemberViewModel(PartyPlayerInfo member, PartyClient partyClient)
        {
            Id = member.Id;
            Name = member.Name;
            IsLeader = member.IsLeader;
            IsSelf = partyClient.IsSelf(member);

            KickCommand = new AsyncRelayCommand(
                () => partyClient.KickMember(Id),
                () => partyClient.IsPartyLeader && !IsSelf);

            PromoteLeaderCommand = new AsyncRelayCommand(
                () => partyClient.PromoteLeader(Id),
                () => partyClient.IsPartyLeader && !IsSelf);
        }
    }

    public sealed partial class PartyViewModel : ObservableObject, IDisposable
    {
        private readonly PartyClient _partyClient;
        private readonly DialogService _dialogService;
        private readonly IErrorHandlingService _errorHandlingService;

        #region Bindings

        public string Status
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
                    return $"Joined ({Members.Count})";
                }

                return HasOtherMembers ? $"Open ({Members.Count})" : "Open";
            }
        }

        public bool IsPartyActive => _partyClient.IsPartyActive;
        public bool IsPartyLeader => _partyClient.IsPartyLeader;
        public bool IsPartyGuest => IsPartyActive && !IsPartyLeader;
        public bool HasOtherMembers => IsPartyActive && Members.Count > 1;

        public string? PartyId => _partyClient.PartyId;
        public ObservableCollection<PartyMemberViewModel> Members { get; } = [];

        #endregion

        public PartyViewModel(PartyClient partyService, DialogService dialogService, IErrorHandlingService errorHandlingService)
        {
            _dialogService = dialogService;
            _errorHandlingService = errorHandlingService;
            _partyClient = partyService;
            _partyClient.PartyChanged += PartyService_PartyChanged;
            _partyClient.PartyClosed += PartyService_PartyClosed;
            _partyClient.KickedFromParty += PartyService_KickedFromParty;
            _partyClient.UserChanged += PartyService_UserChanged;
            _partyClient.UserJoined += PartyService_UserJoined;
            _partyClient.UserLeft += PartyService_UserLeft;
            _partyClient.LeaderChanged += PartyClient_LeaderChanged;
            _partyClient.ConnectionChanged += PartyService_ConnectionChanged;
        }


        #region Event handlers

        private void PartyService_ConnectionChanged(bool connected)
        {
            if (!connected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Members.Clear();

                    OnPropertyChanged(nameof(PartyId));
                    OnPropertyChanged(nameof(IsPartyLeader));
                    OnPropertyChanged(nameof(IsPartyActive));
                    OnPropertyChanged(nameof(IsPartyGuest));
                    OnPropertyChanged(nameof(HasOtherMembers));
                    OnPropertyChanged(nameof(Status));

                    _dialogService.OpenTextDialog("Party", "Connection to party was lost.");
                });
            }
        }

        private void PartyService_PartyClosed()
        {
            Application.Current.Dispatcher.InvokeAsync(() => _dialogService.OpenTextDialog("Party", "Party was closed!"));
        }

        private void PartyService_KickedFromParty()
        {
            Application.Current.Dispatcher.Invoke(() => _dialogService.OpenTextDialog("Party", "You were kicked from the party!"));
        }

        private void PartyService_UserJoined(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddMember(member);

                OnPropertyChanged(nameof(HasOtherMembers));
                OnPropertyChanged(nameof(Status));
            });
        }

        private void PartyService_UserLeft(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int index = Members.IndexOfFirst(m => m.Id == member.Id);
                if (index != -1)
                {
                    Members.RemoveAt(index);
                }

                OnPropertyChanged(nameof(HasOtherMembers));
                OnPropertyChanged(nameof(Status));
            });
        }

        private void PartyService_UserChanged(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PartyMemberViewModel? memberViewModel = Members.FirstOrDefault(m => m.Id == member.Id);
                if (memberViewModel is not null)
                {
                    memberViewModel.Name = member.Name;
                    memberViewModel.IsLeader = member.IsLeader;
                    memberViewModel.IsSelf = _partyClient.IsSelf(member);
                }
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

                foreach (PartyMemberViewModel memberViewModel in Members)
                {
                    memberViewModel.KickCommand.NotifyCanExecuteChanged();
                    memberViewModel.PromoteLeaderCommand.NotifyCanExecuteChanged();
                }
            });
        }

        private void PartyService_PartyChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Members.Clear();

                if (_partyClient.IsPartyActive)
                {
                    foreach (PartyPlayerInfo member in _partyClient.Members)
                    {
                        AddMember(member);
                    }
                }

                OnPropertyChanged(nameof(PartyId));
                OnPropertyChanged(nameof(IsPartyLeader));
                OnPropertyChanged(nameof(IsPartyActive));
                OnPropertyChanged(nameof(IsPartyGuest));
                OnPropertyChanged(nameof(HasOtherMembers));
                OnPropertyChanged(nameof(Status));
            });
        }


        #endregion

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

        private void AddMember(PartyPlayerInfo member)
        {
            PartyMemberViewModel memberViewModel = new(member, _partyClient);

            if (memberViewModel.IsSelf)
            {
                // make sure self is always the first one
                Members.Insert(0, memberViewModel);
            }
            else
            {
                Members.Add(memberViewModel);
            }

            OnPropertyChanged(nameof(HasOtherMembers));
        }

        public void Dispose()
        {
            _partyClient.PartyChanged -= PartyService_PartyChanged;
            _partyClient.PartyClosed -= PartyService_PartyClosed;
            _partyClient.KickedFromParty -= PartyService_KickedFromParty;
            _partyClient.UserChanged -= PartyService_UserChanged;
            _partyClient.UserJoined -= PartyService_UserJoined;
            _partyClient.UserLeft -= PartyService_UserLeft;
            _partyClient.LeaderChanged -= PartyClient_LeaderChanged;
            _partyClient.ConnectionChanged -= PartyService_ConnectionChanged;
        }
    }
}
