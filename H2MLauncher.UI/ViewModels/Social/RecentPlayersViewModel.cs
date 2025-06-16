using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;
using H2MLauncher.Core.Social.Player;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public sealed partial class RecentPlayersViewModel : ObservableObject
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

            Players.Add(new(new ServerConnectionDetails("212.232.18.45", 7780), partyClient, socialClient, dialogService, WeakReferenceMessenger.Default)
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test1",
                UserName = "FunnyBloke39",
                ServerName = "[4XP] [EU] Sniper Lobby Best Maps 24/7",
                EncounteredAt = DateTime.Now.AddMinutes(-5)
            });

            Players.Add(new(new ServerConnectionDetails("212.232.18.45", 7780), partyClient, socialClient, dialogService, WeakReferenceMessenger.Default)
            {
                Id = Guid.NewGuid().ToString(),
                Name = "CoolUser",
                UserName = "CryingYeti84",
                ServerName = "[EU] ^1Rasselbande ^7 | ^1Vanilla KC/ TDM ^7 | ^1MW2 / MW3 Best Maps",
                EncounteredAt = DateTime.Now.AddMinutes(-10)
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
    }
}
