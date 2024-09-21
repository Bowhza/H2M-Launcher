using H2MLauncher.Core.Models;

namespace MatchmakingServer;

public class Party
{
    private readonly HashSet<Player> _members = [];
    private bool _isClosed;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public SimpleServerInfo? Server { get; set; }

    public required Player Leader { get; init; }

    public IReadOnlySet<Player> Members => _members;

    public void AddPlayer(Player player)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();
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

    private void ValidateClosed()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("Party is closed");
        }
    }

    public bool RemovePlayer(Player player)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();

            if (_members.Remove(player))
            {
                player.Party = null;
                return true;
            }

            return false;
        }
    }

    public int CloseParty()
    {
        ValidateClosed();

        int count = 0;

        lock (_members)
        {
            ValidateClosed();

            _isClosed = true;

            foreach (Player member in _members)
            {
                member.Party = null;
                count++;
            }

            _members.Clear();
        }

        return count;
    }
}
