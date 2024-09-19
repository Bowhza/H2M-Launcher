using System.Collections.Concurrent;

namespace MatchmakingServer.SignalR;

public class PlayerStore
{
    public readonly ConcurrentDictionary<string, Player> ConnectedPlayers = [];
}
