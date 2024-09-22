using H2MLauncher.Core.Matchmaking.Models;

namespace MatchmakingServer.SignalR;

public class PlayerStore
{
    // Maps user id to player and connections
    private readonly Dictionary<string, PlayerConnectionInfo> _connectedPlayers = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly struct PlayerConnectionInfo(string userId, Player player)
    {
        public string UserId { get; } = userId;

        public Player Player { get; } = player;

        public HashSet<string> Connections { get; } = [];
    }

    public async Task<Player> GetOrAdd(string userId, string connectionId, string playerName)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_connectedPlayers.TryGetValue(userId, out PlayerConnectionInfo connectionInfo))
            {
                connectionInfo.Connections.Add(connectionId);
                return connectionInfo.Player;
            }

            Player player = new()
            {
                Id = userId,
                Name = playerName,
                State = PlayerState.Connected
            };

            connectionInfo = new(userId, player);
            connectionInfo.Connections.Add(connectionId);

            _connectedPlayers.TryAdd(userId, connectionInfo);

            return player;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Player?> TryRemove(string userId, string connectionId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_connectedPlayers.TryGetValue(userId, out PlayerConnectionInfo connectionInfo))
            {
                return null;
            }

            connectionInfo.Connections.Remove(connectionId);
            if (connectionInfo.Connections.Count == 0)
            {
                _connectedPlayers.Remove(userId);
            }

            connectionInfo.Player.State = PlayerState.Disconnected;

            return connectionInfo.Player;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
