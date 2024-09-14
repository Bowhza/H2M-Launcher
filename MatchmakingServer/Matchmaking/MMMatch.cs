namespace MatchmakingServer
{
    internal record struct MMMatch(GameServer Server, double MatchQuality, List<MMPlayer> SelectedPlayers)
    {
        public static implicit operator (GameServer server, double matchQuality, List<MMPlayer> selectedPlayers)(MMMatch value)
        {
            return (value.Server, value.MatchQuality, value.SelectedPlayers);
        }

        public static implicit operator MMMatch((GameServer server, double matchQuality, List<MMPlayer> selectedPlayers) value)
        {
            return new MMMatch(value.server, value.matchQuality, value.selectedPlayers);
        }
    }
}
