using System.Collections.ObjectModel;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Party;

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
    }

    public partial class PartyViewModel : ObservableObject
    {
        private readonly PartyService _partyService;

        #region Bindings

        public bool IsPartyActive => _partyService.IsPartyActive;
        public bool IsPartyLeader => _partyService.IsPartyLeader;
        public string? PartyId => _partyService.PartyId;
        public ObservableCollection<PartyMemberViewModel> Members { get; } = [];

        #endregion

        public PartyViewModel(PartyService partyService)
        {
            _partyService = partyService;
            _partyService.PartyChanged += PartyService_PartyChanged;
        }

        private void PartyService_PartyChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // todo (tb): do dynamic update instead of clearing whole list
                Members.Clear();
                if (_partyService.IsPartyActive)
                {
                    foreach (PartyPlayerInfo member in _partyService.Members)
                    {
                        Members.Add(new PartyMemberViewModel()
                        {
                            Id = member.Id,
                            Name = member.Name,
                            IsLeader = member.IsLeader,
                            IsSelf = member.Id == _partyService.CurrentClientId
                        });
                    }
                }

                OnPropertyChanged(nameof(PartyId));
                OnPropertyChanged(nameof(IsPartyLeader));
                OnPropertyChanged(nameof(IsPartyActive));
            });
        }

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
    }
}
