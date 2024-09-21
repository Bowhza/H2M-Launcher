using H2MLauncher.Core.Models;

namespace MatchmakingServer;

public class Party
{
    private readonly HashSet<Player> _members = [];

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public SimpleServerInfo? Server { get; set; }

    public required Player Leader { get; init; }

    public IReadOnlySet<Player> Members => _members;

    public void AddPlayer(Player player)
    {
        lock (_members)
        {
            if (player.Party is not null)
            {
                throw new ArgumentException("Cannot add player to party. Player is already in a party.", nameof(player));
            }

            if (!_members.Add(player))
            {
                throw new ArgumentException("Cannot add player to party. Player is already in this party.", nameof(player));
            }

            player.Party = this;
        }
    }

    public bool RemovePlayer(Player player)
    {
        lock (_members)
        {
            if (_members.Remove(player))
            {
                player.Party = null;
                return true;
            }

            return false;
        }
    }
}
