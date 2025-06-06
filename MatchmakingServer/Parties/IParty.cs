
using H2MLauncher.Core.Party;

namespace MatchmakingServer.Parties
{
    public interface IParty
    {
        string Id { get; init; }
        PartyPrivacy Privacy { get; }
        Player Leader { get; }
        IReadOnlySet<Player> Members { get; }

        IReadOnlyDictionary<string, DateTime> Invites { get; }
        IEnumerable<string> ValidInvites { get; }
    }
}