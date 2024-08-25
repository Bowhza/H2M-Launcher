using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails
    {
        public string ServerName { get; }

        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }
        public LinkedList<Player> PlayerQueue { get; } = [];

        public int JoiningPlayerCount { get; set; }

        string IServerConnectionDetails.Ip => ServerIp;

        int IServerConnectionDetails.Port => ServerPort;

        public DateTimeOffset LastSuccessfulPingTimestamp { get; set; }
        public GameServerInfo? LastServerInfo { get; set; }

        public List<string> ActualPlayers { get; } = [];

        public string InstanceId { get; }

        public GameServer(string serverName, string instanceId)
        {
            ServerName = serverName;
            InstanceId = instanceId;
        }

        public override string ToString()
        {
            return $"{ServerName} ({ServerIp}:{ServerPort})";
        }
    }
}
