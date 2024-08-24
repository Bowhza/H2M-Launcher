using System.Collections.Concurrent;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    public class QueueingHub : Hub<IClient>
    {
        private readonly ConcurrentDictionary<string, string> _connectedPlayers = [];

        public async Task<bool> JoinQueue(string serverIp, int serverPort, string playerName)
        {
            if (!_connectedPlayers.TryAdd(Context.ConnectionId, playerName))
            {
                return false;
            }

            return true;
        }
    }

    public interface IClient
    {
        
    }
}
