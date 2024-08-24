using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails
    {
        public string ServerName { get; }

        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }
        public SpecialQueue<Player> PlayerQueue { get; } = [];

        public int ReservedSlots { get; set; }

        string IServerConnectionDetails.Ip => ServerIp;

        int IServerConnectionDetails.Port => ServerPort;

        public DateTimeOffset LastSuccessfulPingTimestamp { get; set; } = DateTime.MinValue;
        public GameServerInfo? LastServerInfo { get; set; }

        public IReadOnlyList<string> ActualPlayers { get; } = [];

        public GameServer(string serverName)
        {
            ServerName = serverName;
        }

    }
}
