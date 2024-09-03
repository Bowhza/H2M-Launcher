using System.Net;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails
    {
        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }

        public ConcurrentLinkedQueue<Player> PlayerQueue { get; } = [];

        public IEnumerable<Player> JoiningPlayers => PlayerQueue.Where(p => p.State is PlayerState.Joining);

        public IEnumerable<Player> QueuedPlayers => PlayerQueue.Where(p => p.State is PlayerState.Queued);

        public int JoiningPlayerCount { get; set; }

        string IServerConnectionDetails.Ip => ServerIp;

        int IServerConnectionDetails.Port => ServerPort;

        public DateTimeOffset LastSuccessfulPingTimestamp { get; set; }
        public GameServerInfo? LastServerInfo { get; set; }

        public List<string> ActualPlayers { get; } = [];

        public string InstanceId { get; }


        public DateTimeOffset SpawnDate { get; init; } = DateTimeOffset.Now;

        public Task? ProcessingTask { get; set; }

        public CancellationTokenSource ProcessingCancellation { get; set; } = new();

        public QueueProcessingState ProcessingState { get; set; } = QueueProcessingState.Stopped;

        public AutoResetEvent PlayersAvailable { get; } = new(false);

        public int PrivilegedSlots { get; init; }

        public int UnavailableSlots => JoiningPlayerCount + PrivilegedSlots;

        public GameServer(string instanceId)
        {
            InstanceId = instanceId;
        }

        /// <summary>
        /// Gets the actual ip address from the game server info if present.
        /// </summary>
        /// <returns></returns>
        public string GetActualIpAddress()
        {
            if (LastServerInfo is not null)
            {
                IPEndPoint endpoint = LastServerInfo.Address;

                return endpoint.Address.IsIPv4MappedToIPv6
                    ? endpoint.Address.MapToIPv4().ToString()
                    : endpoint.Address.ToString();
            }

            return ServerIp;
        }

        public override string ToString()
        {
            return $"[{ServerIp}:{ServerPort}] [{ProcessingState}]";
        }
    }
}
