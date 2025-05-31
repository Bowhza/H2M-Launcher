using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Social;
using H2MLauncher.UI.Dialog;

using MatchmakingServer.Core.Social;


namespace H2MLauncher.UI.ViewModels;

public sealed partial class FriendRequestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private DateTimeOffset _created;

    [ObservableProperty]
    private FriendRequestStatus _status;

    public string Id { get; }

    public IAsyncRelayCommand AcceptRequestCommand { get; }
    public IAsyncRelayCommand RejectRequestCommand { get; }

    public FriendRequestViewModel(string id, SocialClient socialClient, DialogService dialogService)
    {
        Id = id;

        AcceptRequestCommand = new AsyncRelayCommand(async () =>
        {
            if (!await socialClient.AcceptFriendAsync(Id))
            {
                Application.Current.Dispatcher.Invoke(() =>
                        dialogService.OpenTextDialog("Error", $"Could not accept friend {Name}!"));
            }
        });

        RejectRequestCommand = new AsyncRelayCommand(async () =>
        {
            if (!await socialClient.RejectFriendAsync(Id))
            {
                Application.Current.Dispatcher.Invoke(() =>
                        dialogService.OpenTextDialog("Error", $"Could not accept friend {Name}!"));
            }
        });
    }
}
