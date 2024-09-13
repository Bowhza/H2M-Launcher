using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Models;

namespace H2MLauncher.UI.ViewModels
{
    internal partial class MatchmakingPreferencesViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _maxPing = 999;

        [ObservableProperty]
        private int _minPlayers = 8;

        [ObservableProperty]
        private int _maxScore = -1;

        [ObservableProperty]
        private int _maxPlayersOnServer = -1;

        [ObservableProperty]
        private bool _findFreshMatch = false;
        public MatchmakingPreferences ToModel()
        {
            return new()
            {
                SearchCriteria = new()
                {
                    MaxPing = MaxPing,
                    MinPlayers = MinPlayers,
                    MaxScore = MaxScore,
                    MaxPlayersOnServer = MaxPlayersOnServer,
                },
                TryFreshGamesFirst = FindFreshMatch
            };
        }
    }
}
