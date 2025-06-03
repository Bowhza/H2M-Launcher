using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.Social;
using H2MLauncher.UI.Converters;
using H2MLauncher.UI.Dialog;

using MatchmakingServer.Core.Social;

using Refit;

namespace H2MLauncher.UI.ViewModels;

public sealed partial class FriendRequestsViewModel : ObservableObject, IDisposable
{
    private readonly SocialClient _socialClient;
    private readonly DialogService _dialogService;
    private readonly IErrorHandlingService _errorHandlingService;

    private readonly IDisposable _searchSubscription;

    public ObservableCollection<FriendRequestViewModel> Requests { get; } = [];
    public bool HasRequests => Requests.Count > 0;

    [ObservableProperty]
    private int _numIncomingRequests;

    public ICollectionView RequestsGrouped { get; private set; }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private UserSearchResultViewModel[] _searchResults = [];

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

        // 1. Create an observable from the SearchText property changes
        IObservable<string> searchTextObservable = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler
            )
            .Where(e => e.EventArgs.PropertyName == nameof(SearchText))
            .Select(e => ((FriendRequestsViewModel)e.Sender!).SearchText)
            .Throttle(TimeSpan.FromMilliseconds(500)) // Debounce input
            .DistinctUntilChanged() // Only push events if the text actually changed
            .Do(_ =>
            {
                // Clear previous results while waiting for new ones
                Application.Current.Dispatcher.Invoke(() => SearchResults = []);
            })
            .Where(text => !string.IsNullOrWhiteSpace(text) && text.Length >= 3); // Minimum query length
            

        // 2. Subscribe to the debounced observable and perform the search
        _searchSubscription = searchTextObservable
            .SelectMany(query => Observable.FromAsync(async (ct) => 
            {
                try
                {
                    return await PerformUserSearch(query, ct);
                }
                catch (OperationCanceledException)
                {
                    // Request was cancelled, just return empty to indicate no new results
                    return null;
                }
                catch (Exception)
                {
                    // Handle other API call errors
                    return null;
                }
            }))
            .ObserveOn(SynchronizationContext.Current!) // Ensure results are processed on the UI thread
            .Subscribe(results =>
            {
                // Update the UI with results
                if (results is not null)
                {
                    SearchResults = results.Select(r => new UserSearchResultViewModel
                    {
                        Id = r.Id,
                        UserName = r.UserName,
                        PlayerName = r.PlayerName
                    }).ToArray();
                }
            });
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
            return;
        }

        foreach (UserSearchResultViewModel searchResultViewModel in SearchResults.Where(r => r.Id == friendId))
        {
            searchResultViewModel.HasRequested = true;
        }
    }

    private async Task<IEnumerable<UserSearchResultDto>> PerformUserSearch(string query, CancellationToken cancellationToken)
    {
        IApiResponse<List<UserSearchResultDto>> response = await _socialClient.FriendshipApi.SearchFriendsAsync(query, cancellationToken);

        if (response.IsSuccessful)
        {
            return response.Content.Where(user => 
                !_socialClient.FriendRequests.Any(r => r.UserId.ToString() == user.Id) && // not already requested
                !_socialClient.Friends.Any(r => r.Id == user.Id) && // not already friends
                _socialClient.Context.UserId != user.Id.ToString() // not self
            );
        }

        return [];
    }

    public void Dispose()
    {
        _socialClient.FriendRequestsChanged -= SocialClient_FriendRequestsChanged;

        _searchSubscription.Dispose();
    }
}
