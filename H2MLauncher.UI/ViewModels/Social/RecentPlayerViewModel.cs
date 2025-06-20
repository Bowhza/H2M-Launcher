using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Messages;

namespace H2MLauncher.UI.ViewModels
{
    public sealed partial class RecentPlayerViewModel : ObservableObject
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string UserName { get; init; }

        public required string ServerName { get; init; }

        public required DateTime EncounteredAt { get; init; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddFriendCommand))]
        private bool _hasFriendRequested = false;

        public IRelayCommand SelectServerCommand { get; }

        public IAsyncRelayCommand InviteToPartyCommand { get; }
        public IAsyncRelayCommand AddFriendCommand { get; }

        public IRelayCommand CopyUserIdCommand { get; }
        public IRelayCommand CopyUserNameCommand { get; }

        public RecentPlayerViewModel(IServerConnectionDetails server, PartyClient partyClient, SocialClient socialClient, DialogService dialogService, IMessenger messenger)
        {
            SelectServerCommand = new RelayCommand(() => messenger.Send(new SelectServerMessage(server)));

            InviteToPartyCommand = new AsyncRelayCommand(
                async () =>
                {
                    InviteInfo? invite = await partyClient.InviteToParty(Id!);
                    if (invite is not null)
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                               dialogService.OpenTextDialog("Social", $"Sent invite to {Name!}!"));
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                           dialogService.OpenTextDialog("Error", $"Could not send invite to {Name}!"));
                    }
                },
                () => partyClient.IsPartyActive);

            AddFriendCommand = new AsyncRelayCommand(
                async () =>
                {
                    if (await socialClient.AddFriendAsync(Id!))
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            dialogService.OpenTextDialog("Social", $"Sent friend request to {Name!}!"));

                        HasFriendRequested = true;
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            dialogService.OpenTextDialog("Error", $"Could not send friend request to {Name}!"));
                    }
                },
                () => !HasFriendRequested);

            CopyUserIdCommand = new RelayCommand(() => Clipboard.SetText(Id));
            CopyUserNameCommand = new RelayCommand(() => Clipboard.SetText(UserName), () => !string.IsNullOrEmpty(UserName));
        }
    }
}
