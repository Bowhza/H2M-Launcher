﻿using H2MLauncher.Core.Social.Status;

namespace MatchmakingServer.Social
{
    /// <summary>
    /// Tracks the gamer servers clients are playing on.
    /// </summary>
    public interface IPlayerServerTrackingService
    {
        IReadOnlyCollection<Player> TrackedPlayers { get; }
        IReadOnlyCollection<GameServer> TrackedServers { get; }

        event Action<Player, GameServer>? PlayerJoinedServer;
        event Action<PlayerServerTrackingService.PlayerLeftEventArgs>? PlayerLeftServer;
        event Action<GameServer>? ServerTimeout;
        event Action<GameServer>? ServerRefreshed;

        /// <summary>
        /// Handles server connection updates sent by the client for the given <paramref name="player"/> by finding a matching server
        /// and starting or stopping tracking.
        /// </summary>
        /// <param name="player">The player associated with the connection update.</param>
        /// <param name="connectedServerInfo">The info about the connected server or <see langword="null"/> if disconnected.</param>
        Task HandlePlayerConnectionUpdate(Player player, ConnectedServerInfo? connectedServerInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Removes the player from it's current server and stops tracking him.
        /// </summary>
        Task<bool> RemovePlayerFromCurrentServer(Player player);
    }
}