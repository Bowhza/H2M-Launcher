using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.Social;
using H2MLauncher.UI.Converters;
using H2MLauncher.UI.Dialog;

using MatchmakingServer.Core.Social;

namespace H2MLauncher.UI.ViewModels;

public sealed partial class FriendRequestsViewModel : ObservableObject, IDisposable
{
    private readonly SocialClient _socialClient;
    private readonly DialogService _dialogService;
    private readonly IErrorHandlingService _errorHandlingService;

    public ObservableCollection<FriendRequestViewModel> Requests { get; } = [];
    public bool HasRequests => Requests.Count > 0;

    [ObservableProperty]
    private int _numIncomingRequests;

    public ICollectionView RequestsGrouped { get; private set; }

    public FriendRequestsViewModel(SocialClient socialClient, DialogService dialogService, IErrorHandlingService errorHandlingService)
    {
        _dialogService = dialogService;
        _errorHandlingService = errorHandlingService;
        _socialClient = socialClient;
        _socialClient.FriendRequestsChanged += SocialClient_FriendRequestsChanged;

        RequestsGrouped = CollectionViewSource.GetDefaultView(Requests);

        RequestsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendRequestViewModel.Status), ListSortDirection.Ascending));
        RequestsGrouped.SortDescriptions.Add(new SortDescription(nameof(FriendRequestViewModel.Created), ListSortDirection.Descending));

        // Finally, group by the Status property
        RequestsGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FriendRequestViewModel.Status), new FriendRequestStatusConverter()));
    }

    private void SocialClient_FriendRequestsChanged()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Requests.Clear();

            foreach (FriendRequestDto friendRequest in _socialClient.FriendRequests)
            {
                Requests.Add(new FriendRequestViewModel(friendRequest.UserId.ToString(), _socialClient, _dialogService)
                {
                    UserName = friendRequest.UserName,
                    Name = friendRequest.PlayerName ?? friendRequest.UserName,
                    Created = friendRequest.Created,
                    Status = friendRequest.Status,
                });
            }

            NumIncomingRequests = Requests.Where(r => r.Status is FriendRequestStatus.PendingIncoming).Count();

            OnPropertyChanged(nameof(HasRequests));
        });
    }

    [RelayCommand]
    public async Task AddFriend(string? friendId)
    {
        friendId ??= _dialogService.OpenInputDialog(
            title: "Add Friend",
            text: "Enter or paste the ID of the friend to add:",
            acceptButtonText: "Send");

        if (friendId is null)
        {
            return;
        }

        if (!Guid.TryParse(friendId, out _))
        {
            _errorHandlingService.HandleError("Invalid friend id.");
            return;
        }

        if (!await _socialClient.AddFriendAsync(friendId))
        {
            _errorHandlingService.HandleError("Could not add friend.");
        }
    }

    public void Dispose()
    {
        _socialClient.FriendRequestsChanged -= SocialClient_FriendRequestsChanged;
    }

}
