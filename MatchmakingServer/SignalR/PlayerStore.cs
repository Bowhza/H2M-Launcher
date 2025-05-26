using H2MLauncher.Core.Matchmaking.Models;

using MatchmakingServer.Database.Migrations;

namespace MatchmakingServer.SignalR;

public class PlayerStore
{
    // Maps user id to player and connections
    private readonly Dictionary<string, PlayerConnectionInfo> _connectedPlayers = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Maps user id to last connection time
    private readonly Dictionary<string, DateTimeOffset> _lastConnections = [];

    public int NumConnectedPlayers => _connectedPlayers.Count;
    public int NumPlayersSeen => _lastConnections.Count;
    public int NumPlayersSeenToday => _lastConnections.Values.Where(time => time.DayOfYear == DateTimeOffset.Now.DayOfYear).Count();

    private readonly struct PlayerConnectionInfo(Player player)
    {
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
                _lastConnections[userId] = DateTimeOffset.Now;
                return connectionInfo.Player;
            }

            Player player = new()
            {
                Id = userId,
                Name = playerName,
                State = PlayerState.Connected
            };

            connectionInfo = new(player);
            connectionInfo.Connections.Add(connectionId);

            _connectedPlayers.TryAdd(userId, connectionInfo);
            _lastConnections[userId] = DateTimeOffset.Now;

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
                connectionInfo.Player.State = PlayerState.Disconnected;
            }

            return connectionInfo.Player;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IList<Player>> GetAllPlayers()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _connectedPlayers.Values.Select(connectionInfo => connectionInfo.Player).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Player?> TryGet(string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_connectedPlayers.TryGetValue(userId, out PlayerConnectionInfo info))
            {
                return info.Player;
            }

            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Clear()
    {
        await _semaphore.WaitAsync();
        try
        {
            _connectedPlayers.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
