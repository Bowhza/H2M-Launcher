using System.Collections.ObjectModel;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core;
using H2MLauncher.Core.Party;
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

        public required string Id { get; init; }

        public required IAsyncRelayCommand KickCommand { get; init; }
    }

    public sealed partial class PartyViewModel : ObservableObject, IDisposable
    {
        private readonly PartyClient _partyClient;
        private readonly DialogService _dialogService;

        #region Bindings

        public bool IsPartyActive => _partyClient.IsPartyActive;
        public bool IsPartyLeader => _partyClient.IsPartyLeader;
        public string? PartyId => _partyClient.PartyId;
        public ObservableCollection<PartyMemberViewModel> Members { get; } = [];

        #endregion

        public PartyViewModel(PartyClient partyService, DialogService dialogService)
        {
            _dialogService = dialogService;
            _partyClient = partyService;
            _partyClient.PartyChanged += PartyService_PartyChanged;
            _partyClient.PartyClosed += PartyService_PartyClosed;
            _partyClient.KickedFromParty += PartyService_KickedFromParty;
            _partyClient.UserChanged += PartyService_UserChanged;
            _partyClient.UserJoined += PartyService_UserJoined;
            _partyClient.UserLeft += PartyService_UserLeft;
            _partyClient.ConnectionChanged += PartyService_ConnectionChanged;
        }


        #region Event handlers

        private void PartyService_ConnectionChanged(bool connected)
        {
            if (!connected)
            {
                _dialogService.OpenTextDialog("Party", "Connection to party was lost.");

                Members.Clear();
                OnPropertyChanged(nameof(PartyId));
                OnPropertyChanged(nameof(IsPartyLeader));
                OnPropertyChanged(nameof(IsPartyActive));
            }
        }

        private void PartyService_PartyClosed()
        {
            _dialogService.OpenTextDialog("Party", "Party was closed!");
        }

        private void PartyService_KickedFromParty()
        {
            _dialogService.OpenTextDialog("Party", "You were kicked from the party!");
        }

        private void PartyService_UserJoined(PartyPlayerInfo member)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddMember(member);
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
            });
        }


        #endregion

        [RelayCommand]
        public Task CreateParty()
        {
            if (_partyClient.IsPartyActive)
            {
                return Task.CompletedTask;
            }

            return _partyClient.CreateParty();
        }

        [RelayCommand]
        public Task JoinParty(string partyId)
        {
            return _partyClient.JoinParty(partyId);
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

        public Task KickPlayer(string id)
        {
            return _partyClient.KickMember(id);
        }

        private void AddMember(PartyPlayerInfo member)
        {
            Members.Add(new PartyMemberViewModel()
            {
                Id = member.Id,
                Name = member.Name,
                IsLeader = member.IsLeader,
                IsSelf = _partyClient.IsSelf(member),
                KickCommand = new AsyncRelayCommand(
                        () => KickPlayer(member.Id),
                        () => _partyClient.IsPartyLeader && !_partyClient.IsSelf(member))
            });
        }

        public void Dispose()
        {
            _partyClient.PartyChanged -= PartyService_PartyChanged;
            _partyClient.PartyClosed -= PartyService_PartyClosed;
            _partyClient.KickedFromParty -= PartyService_KickedFromParty;
            _partyClient.UserChanged -= PartyService_UserChanged;
            _partyClient.UserJoined -= PartyService_UserJoined;
            _partyClient.UserLeft -= PartyService_UserLeft;
            _partyClient.ConnectionChanged -= PartyService_ConnectionChanged;
        }
    }
}
