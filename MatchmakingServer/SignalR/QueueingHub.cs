﻿using System.Collections.Concurrent;

using H2MLauncher.Core.Services;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    public class QueueingHub : Hub<IClient>, IQueueingHub
    {
        private readonly ILogger<QueueingHub> _logger;
        private readonly QueueingService _queueingService;
        private readonly MatchmakingService _matchmakingService;

        private readonly ConcurrentDictionary<string, Player> _connectedPlayers = [];

        public QueueingHub(ILogger<QueueingHub> logger, QueueingService queueingService, MatchmakingService matchmakingService)
        {
            _logger = logger;
            _queueingService = queueingService;
            _matchmakingService = matchmakingService;
        }


        /// <summary>
        /// Register a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <param name="playerName">Player name</param>
        /// <returns>Whether sucessfully registered.</returns>
        private Player GetOrAddPlayer(string connectionId, string playerName)
        {
            if (_connectedPlayers.TryGetValue(connectionId, out Player? player))
            {
                return player;
            }

            player = new()
            {
                Name = playerName,
                ConnectionId = connectionId,
                State = PlayerState.Connected
            };

            if (_connectedPlayers.TryAdd(connectionId, player))
            {
                _logger.LogInformation("Connected player: {player}", player);
            }

            return player;
        }

        /// <summary>
        /// Unregister a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <returns>The previously connected player.</returns>
        private Player? RemovePlayer(string connectionId)
        {
            if (!_connectedPlayers.TryRemove(connectionId, out var player))
            {
                return null;
            }

            // clean up
            _queueingService.LeaveQueue(player, disconnected: true);

            if (player.State is PlayerState.Matchmaking)
            {
                _matchmakingService.LeaveMatchmaking(player);
            }

            return player;
        }

        public Task JoinAck(bool successful)
        {
            if (!_connectedPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                // not found
                return Task.CompletedTask;
            }

            if (successful)
            {
                _queueingService.OnPlayerJoinConfirmed(player);
            }
            else
            {
                _queueingService.OnPlayerJoinFailed(player);
            }

            return Task.CompletedTask;
        }

        public Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId, string playerName)
        {
            _logger.LogTrace("JoinQueue({serverIp}:{serverPort}, {playerName}) triggered", serverIp, serverPort, playerName);

            var player = GetOrAddPlayer(Context.ConnectionId, playerName);
            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                // player already in queue
                _logger.LogWarning("Cannot join queue for {serverIp}:{serverPort}, player {player} already queued", 
                    serverIp, serverPort, player);
                return Task.FromResult(false);
            }

            return _queueingService.JoinQueue(serverIp, serverPort, player, instanceId);
        }

        public Task LeaveQueue()
        {
            if (!_connectedPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                // unknown player
                return Task.CompletedTask;
            }

            _queueingService.LeaveQueue(player);

            return Task.CompletedTask;
        }

        public bool SearchMatch(string playerName, int minPlayers, List<string> preferredServers)
        {
            var player = GetOrAddPlayer(Context.ConnectionId, playerName);

            return _matchmakingService.EnterMatchmaking(player, minPlayers, preferredServers);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(exception, "Client disconnected: {connectionId}", Context.ConnectionId);

            var player = RemovePlayer(Context.ConnectionId);
            if (player is null)
            {
                return;
            }

            _logger.LogInformation("Removed player {player}", player);

            await Task.CompletedTask;
        }
    }
}
