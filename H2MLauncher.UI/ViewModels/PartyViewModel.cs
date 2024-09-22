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
        private readonly PartyService _partyService;
        private readonly DialogService _dialogService;

        #region Bindings

        public bool IsPartyActive => _partyService.IsPartyActive;
        public bool IsPartyLeader => _partyService.IsPartyLeader;
        public string? PartyId => _partyService.PartyId;
        public ObservableCollection<PartyMemberViewModel> Members { get; } = [];

        #endregion

        public PartyViewModel(PartyService partyService, DialogService dialogService)
        {
            _dialogService = dialogService;
            _partyService = partyService;
            _partyService.PartyChanged += PartyService_PartyChanged;
            _partyService.PartyClosed += PartyService_PartyClosed;
            _partyService.KickedFromParty += PartyService_KickedFromParty;
            _partyService.UserChanged += PartyService_UserChanged;
            _partyService.UserJoined += PartyService_UserJoined;
            _partyService.UserLeft += PartyService_UserLeft;
            _partyService.ConnectionChanged += PartyService_ConnectionChanged;
        }


        #region Event handlers

        private void PartyService_ConnectionChanged(bool connected)
        {
            if (!connected)
            {
                _dialogService.OpenTextDialog("Party", "Connection to party was lost.");
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

                if (_partyService.IsPartyActive)
                {
                    foreach (PartyPlayerInfo member in _partyService.Members)
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
            if (_partyService.IsPartyActive)
            {
                return Task.CompletedTask;
            }

            return _partyService.CreateParty();
        }

        [RelayCommand]
        public Task JoinParty(string partyId)
        {
            return _partyService.JoinParty(partyId);
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
            return _partyService.LeaveParty();
        }

        public Task KickPlayer(string id)
        {
            return _partyService.KickMember(id);
        }

        private void AddMember(PartyPlayerInfo member)
        {
            Members.Add(new PartyMemberViewModel()
            {
                Id = member.Id,
                Name = member.Name,
                IsLeader = member.IsLeader,
                IsSelf = member.Id == _partyService.CurrentClientId,
                KickCommand = new AsyncRelayCommand(
                        () => KickPlayer(member.Id),
                        () => _partyService.IsPartyLeader && member.Id != _partyService.CurrentClientId)
            });
        }

        public void Dispose()
        {
            _partyService.PartyChanged -= PartyService_PartyChanged;
            _partyService.PartyClosed -= PartyService_PartyClosed;
            _partyService.KickedFromParty -= PartyService_KickedFromParty;
            _partyService.UserChanged -= PartyService_UserChanged;
            _partyService.UserJoined -= PartyService_UserJoined;
            _partyService.UserLeft -= PartyService_UserLeft;
            _partyService.ConnectionChanged -= PartyService_ConnectionChanged;
        }
    }
}
