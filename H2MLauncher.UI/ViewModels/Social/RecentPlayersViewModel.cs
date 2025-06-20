using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;
using H2MLauncher.Core.Social.Player;
using H2MLauncher.UI.Dialog;


namespace H2MLauncher.UI.ViewModels
{
    public sealed partial class RecentPlayersViewModel : ObservableObject, IDisposable
    {
        private readonly PartyClient _partyClient;
        private readonly SocialClient _socialClient;
        private readonly DialogService _dialogService;

        public ObservableCollection<RecentPlayerViewModel> Players { get; } = [];

        public bool HasPlayers => Players.Count > 0;

        public RecentPlayersViewModel(
            PartyClient partyClient,
            SocialClient socialClient,
            DialogService dialogService)
        {
            _partyClient = partyClient;
            _socialClient = socialClient;
            _dialogService = dialogService;

            _socialClient.RecentPlayersChanged += RefreshRecentPlayerList;            

            ICollectionView collectionView = CollectionViewSource.GetDefaultView(Players);
            collectionView.SortDescriptions.Add(new SortDescription(nameof(RecentPlayerViewModel.EncounteredAt), ListSortDirection.Descending));

            RefreshRecentPlayerList();
        }

        private void RefreshRecentPlayerList()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Players.Clear();
                foreach (RecentPlayerInfo recentPlayer in _socialClient.RecentPlayers)
                {
                    AddRecentPlayer(recentPlayer);
                }

                OnPropertyChanged(nameof(HasPlayers));
            });
        }

        private void AddRecentPlayer(RecentPlayerInfo recentPlayerInfo)
        {
            Players.Add(
                new RecentPlayerViewModel(recentPlayerInfo.Server, _partyClient, _socialClient, _dialogService, WeakReferenceMessenger.Default)
                {
                    Id = recentPlayerInfo.Id,
                    Name = recentPlayerInfo.PlayerName ?? recentPlayerInfo.UserName,
                    UserName = recentPlayerInfo.UserName,
                    ServerName = recentPlayerInfo.Server.ServerName,
                    EncounteredAt = recentPlayerInfo.EncounterDate.LocalDateTime,
                });
        }

        public void Dispose()
        {
            _socialClient.RecentPlayersChanged -= RefreshRecentPlayerList;
        }
    }
}
