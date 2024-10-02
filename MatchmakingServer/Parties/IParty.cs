
namespace MatchmakingServer.Parties
{
    public interface IParty
    {
        string Id { get; init; }
        Player Leader { get; }
        IReadOnlySet<Player> Members { get; }
    }
}