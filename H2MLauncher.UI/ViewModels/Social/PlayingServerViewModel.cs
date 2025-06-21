using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Models;

namespace H2MLauncher.UI.ViewModels
{
    public partial class PlayingServerViewModel : ObservableObject, ISimpleServerInfo
    {
        public required string Ip { get; init; }

        public required int Port { get; init; }

        public required string ServerName { get; init; }

        [ObservableProperty]
        private string _mapDisplayName = "";

        [ObservableProperty]
        private string _gameTypeDisplayName = "";

        public required DateTimeOffset JoinedAt { get; init; }

        [ObservableProperty]
        private TimeSpan _playingTime = TimeSpan.Zero;

        public string Status => this switch
        {
            { GameTypeDisplayName: not null, MapDisplayName: not null } =>
                $"{GameTypeDisplayName} on {MapDisplayName}",
            { MapDisplayName: not null } => $"Playing on {MapDisplayName}",
            _ => ""
        };

        public string SanitizedServerName => ColorCodeSequenceRegex().Replace(ServerName, "");


        public void RecalculatePlayingTime()
        {
            PlayingTime = DateTimeOffset.Now - JoinedAt;
        }

        [GeneratedRegex(@"(\^\d)")]
        private static partial Regex ColorCodeSequenceRegex();
    }
}
