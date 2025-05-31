
using H2MLauncher.Core.Party;

namespace MatchmakingServer.Parties
{
    public interface IParty
    {
        string Id { get; init; }
        PartyPrivacy Privacy { get; }
        Player Leader { get; }
        IReadOnlySet<Player> Members { get; }
    }
}