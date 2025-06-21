using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public sealed partial class SocialOverviewViewModel : ObservableObject
    {
        public FriendsViewModel Friends { get; }
        public FriendRequestsViewModel FriendRequests { get; }

        public ObservableCollection<ObservableObject> Tabs { get; }

        public SocialOverviewViewModel(FriendsViewModel friendsViewModel, FriendRequestsViewModel friendRequestsViewModel, RecentPlayersViewModel recentPlayersViewModel)
        {
            Friends = friendsViewModel;
            FriendRequests = friendRequestsViewModel;
            Tabs = [Friends, FriendRequests, recentPlayersViewModel];
        }
    }
}
