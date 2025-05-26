using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Party;
using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

using MatchmakingServer.Core.Social;

namespace H2MLauncher.UI.ViewModels
{
    public sealed partial class FriendsViewModel : ObservableObject, IDisposable
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

                return "Connected";
            }
        }

        public bool HasFriends => Friends.Count > 0;

        public ObservableCollection<FriendViewModel> Friends { get; } = [];

        public ICollectionView FriendsGrouped { get; private set; }

        #endregion

        public FriendsViewModel(PartyClient partyService, DialogService dialogService, IErrorHandlingService errorHandlingService)
        {
            _dialogService = dialogService;
            _errorHandlingService = errorHandlingService;
            _partyClient = partyService;
            _partyClient.ConnectionChanged += PartyService_ConnectionChanged;

            // Populate with some dummy data for testing
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "John", Status = OnlineStatus.Online });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Mike", Status = OnlineStatus.Offline });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Alice", Status = OnlineStatus.InGame, GameStatus = GameStatus.InMatch });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Jane", Status = OnlineStatus.InGame, PartySize = 3, CanJoinParty = true, GameStatus = GameStatus.InMainMenu });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Lucas", Status = OnlineStatus.InGame, PartySize = 1, CanJoinParty = false });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Bob", Status = OnlineStatus.Online });
            Friends.Add(new FriendViewModel(Guid.NewGuid().ToString()) { Name = "Charlie", Status = OnlineStatus.Offline });

            FriendsGrouped = CollectionViewSource.GetDefaultView(Friends);

            // Apply the custom comparer for group sorting
            // This sorting order applies to the groups themselves.
            if (FriendsGrouped is ListCollectionView listCollectionView)
            {
                listCollectionView.CustomSort = new FriendStatusGroupComparer();
            }

            // Then, optionally sort items within each group (e.g., alphabetically by name)
            FriendsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.Name), ListSortDirection.Ascending));
            
            // Finally, group by the Status property
            FriendsGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FriendViewModel.Status)));
        }


        #region Event handlers

        private void PartyService_ConnectionChanged(bool connected)
        {
            if (!connected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Friends.Clear();

                    OnPropertyChanged(nameof(HasFriends));
                    OnPropertyChanged(nameof(Status));

                    _dialogService.OpenTextDialog("Party", "Connection to party was lost.");
                });
            }
        }


        #endregion


        public void Dispose()
        {
            _partyClient.ConnectionChanged -= PartyService_ConnectionChanged;
        }

        public class FriendStatusGroupComparer : IComparer
        {
            private static readonly Dictionary<OnlineStatus, int> StatusOrder = new()
            {
                { OnlineStatus.InGame, 0 },
                { OnlineStatus.Online, 1 },
                { OnlineStatus.Offline, 2 }
            };

            public int Compare(object? x, object? y)
            {
                if (x is FriendViewModel groupX && y is FriendViewModel groupY)
                {
                    int orderX, orderY = int.MaxValue;

                    StatusOrder.TryGetValue(groupX.Status, out orderX);
                    StatusOrder.TryGetValue(groupY.Status, out orderY);

                    return orderX.CompareTo(orderY);
                }

                return 0;
            }
        }
    }
}
