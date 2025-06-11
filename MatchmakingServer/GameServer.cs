using System.Collections.Concurrent;
using System.Linq;

using ConcurrentCollections;

using H2MLauncher.Core;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking.GameServer;

using Nito.AsyncEx;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails, ISimpleServerInfo
    {
        public string Id { get; init; } = "";

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

        #region Queueing

        public ConcurrentLinkedQueue<Player> PlayerQueue { get; } = [];

        /// <summary>
        /// Players that are currently joining this server from the queue.
        /// </summary>
        public IEnumerable<Player> JoiningPlayers => PlayerQueue.Where(p => p.State is PlayerState.Joining);

        /// <summary>
        /// Players that are currently queued to join this server.
        /// </summary>
        public IEnumerable<Player> QueuedPlayers => PlayerQueue.Where(p => p.State is PlayerState.Queued);

        /// <summary>
        /// The number of players currently joining this server from the queue.
        /// </summary>
        public int JoiningPlayerCount { get; set; }

        /// <summary>
        /// When this server object was created.
        /// </summary>
        public DateTimeOffset SpawnDate { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// The current server queue processing task.
        /// </summary>
        public Task? ProcessingTask { get; set; }

        /// <summary>
        /// Cancellation token source used to cancel the queue processing task.
        /// </summary>
        public CancellationTokenSource ProcessingCancellation { get; set; } = new();

        /// <summary>
        /// The queue processing state.
        /// </summary>
        public QueueProcessingState ProcessingState { get; set; } = QueueProcessingState.Stopped;

        /// <summary>
        /// Event that gets triggered when players joined the idle server queue.
        /// </summary>
        public AsyncManualResetEvent PlayersAvailable { get; } = new(false);

        public List<string> ActualPlayers { get; } = [];

        #endregion


        public IReadOnlyDictionary<Player, DateTimeOffset> KnownPlayers
        {
            get
            {
                // return a thread-safe snapshot
                lock (PlayerCollectionLock)
                {
                    return _knownPlayers.ToDictionary();
                }
            }
        }


        private readonly Dictionary<Player, DateTimeOffset> _knownPlayers = [];

        internal readonly object PlayerCollectionLock = new();


        // Internal methods for managing players, ideally only called by ServerManager
        internal bool AddPlayerInternal(Player player)
        {
            lock (PlayerCollectionLock)
            {
                return _knownPlayers.TryAdd(player, DateTimeOffset.Now);
            }
        }

        internal bool RemovePlayerInternal(Player player, out DateTimeOffset startTime)
        {
            lock (PlayerCollectionLock)
            {
                return _knownPlayers.Remove(player, out startTime);
            }
        }

        public bool ContainsPlayer(Player player)
        {
            lock (PlayerCollectionLock)
            {
                return _knownPlayers.ContainsKey(player);
            }
        }



        public DateTimeOffset? LastServerInfoTimestamp { get; set; }
        public DateTimeOffset? LastServerStatusTimestamp { get; set; }
        public GameServerInfo? LastServerInfo { get; set; }
        public GameServerStatus? LastStatusResponse { get; set; }


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

        string IServerConnectionDetails.Ip => ServerIp;
        int IServerConnectionDetails.Port => ServerPort;

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
