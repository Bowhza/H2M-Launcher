using H2MLauncher.Core;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;

using Nito.AsyncEx;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails, ISimpleServerInfo
    {
        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }

        private string? _serverName;
        public string ServerName
        {
            get
            {
                return string.IsNullOrEmpty(_serverName)
                    ? LastServerInfo?.HostName ?? ""
                    : _serverName;
            }
            set
            {
                _serverName = value;
            }
        }

        public ConcurrentLinkedQueue<Player> PlayerQueue { get; } = [];

        public IEnumerable<Player> JoiningPlayers => PlayerQueue.Where(p => p.State is PlayerState.Joining);

        public IEnumerable<Player> QueuedPlayers => PlayerQueue.Where(p => p.State is PlayerState.Queued);

        public int JoiningPlayerCount { get; set; }

        string IServerConnectionDetails.Ip => ServerIp;

        int IServerConnectionDetails.Port => ServerPort;

        public DateTimeOffset LastSuccessfulPingTimestamp { get; set; }
        public GameServerInfo? LastServerInfo { get; set; }
        public GameServerStatus? LastStatusResponse { get; set; }

        public List<string> ActualPlayers { get; } = [];

        public string InstanceId { get; } = "";

        public DateTimeOffset SpawnDate { get; init; } = DateTimeOffset.Now;

        public Task? ProcessingTask { get; set; }

        public CancellationTokenSource ProcessingCancellation { get; set; } = new();

        public QueueProcessingState ProcessingState { get; set; } = QueueProcessingState.Stopped;

        public AsyncManualResetEvent PlayersAvailable { get; } = new(false);

        public int PrivilegedSlots { get; init; }

        public int UnavailableSlots
        {
            get
            {
                if (LastServerInfo is null || LastServerInfo.PrivilegedSlots < 0)
                {
                    return JoiningPlayerCount + PrivilegedSlots;
                }

                return JoiningPlayerCount + LastServerInfo.PrivilegedSlots;
            }
        }

        /// <summary>
        /// Gets the actual ip address from the game server info if present.
        /// </summary>
        /// <returns></returns>
        public string GetActualIpAddress()
        {
            if (LastServerInfo is not null)
            {
                return LastServerInfo.Address.Address.GetRealAddress().ToString();
            }

            return ServerIp;
        }

        public override string? ToString()
        {
            return $"[{ServerIp}:{ServerPort}] [{ProcessingState}]";
        }
    }
}
