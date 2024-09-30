namespace MatchmakingServer
{
    public record struct MMMatch(GameServer Server, double MatchQuality, List<MMTicket> SelectedTickets)
    {
        public static implicit operator (GameServer server, double matchQuality, List<MMTicket> selectedTickets)(MMMatch value)
        {
            return (value.Server, value.MatchQuality, value.SelectedTickets);
        }

        public static implicit operator MMMatch((GameServer server, double matchQuality, List<MMTicket> selectedTickets) value)
        {
            return new MMMatch(value.server, value.matchQuality, value.selectedTickets);
        }
    }
}
