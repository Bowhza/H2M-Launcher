using System.Collections.Concurrent;

using H2MLauncher.Core.Services;

using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    public class QueueingService
    {
        private readonly ConcurrentDictionary<(string ip, int port), GameServer> _servers = [];
        private readonly ConcurrentDictionary<string, Player> _connectedPlayers = [];
        private readonly GameServerCommunicationService<IServerConnectionDetails> _gameServerCommunicationService;
        private readonly IHubContext<QueueingHub, IClient> _ctx;

        public QueueingService(
            GameServerCommunicationService<IServerConnectionDetails> gameServerCommunicationService,
            IHubContext<QueueingHub, IClient> ctx)
        {
            _gameServerCommunicationService = gameServerCommunicationService;
            _ctx = ctx;
        }

        /// <summary>
        /// Register a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <param name="playerName">Player name</param>
        /// <returns>Whether sucessfully registered.</returns>
        public Player? AddPlayer(string connectionId, string playerName)
        {
            Player player = new()
            {
                Name = playerName,
                ConnectionId = connectionId
            };

            if (!_connectedPlayers.TryAdd(connectionId, player))
            {
                return null;
            }

            return player;
        }

        /// <summary>
        /// Unregister a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <returns></returns>
        public Player? RemovePlayer(string connectionId)
        {
            if (!_connectedPlayers.TryRemove(connectionId, out var player))
            {
                return null;
            }

            // clean up
            LeaveQueue(player);

            return player;
        }

        private async Task TryJoinPlayer(Player player, GameServer server)
        {
            // notify client to join
            var joinTriggeredSuccessfully = await _ctx.Clients.Client(player.ConnectionId)
                .NotifyJoinAsync(server.ServerIp, server.ServerPort);

            if (joinTriggeredSuccessfully)
            {
                player.State = PlayerState.Joining;
                server.ReservedSlots++;
            }
        }

        private static void ConfirmJoinedPlayers(GameServer server)
        {
            foreach (Player joiningPlayer in server.PlayerQueue.Where(p => p.State is PlayerState.Joining))
            {
                if (server.ActualPlayers.Contains(joiningPlayer.Name)) // TODO: more sophisticated check
                {
                    if (server.PlayerQueue.Remove(joiningPlayer))
                    {
                        server.ReservedSlots--;
                    }

                    joiningPlayer.State = PlayerState.Joined;
                    joiningPlayer.Server = server;

                    // yippeeeee
                }
            }
        }

        private async Task ProcessQueuedPlayers(GameServer server, CancellationToken cancellationToken)
        {
            while (true)
            {
                using var timeoutCts = new CancellationTokenSource(10000);
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                try
                {
                    // only peek at the queue until player actually joined
                    var nextPlayer = server.PlayerQueue.Peek();
                    if (nextPlayer is null)
                    {
                        // TODO: what to do when queue is empty?
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    var gameServerInfo = await _gameServerCommunicationService.RequestServerInfoAsync(server, linkedCancellation.Token);
                    if (gameServerInfo is null)
                    {
                        // could not send request
                        server.LastServerInfo = null;
                        continue;
                    }
                    else
                    {
                        // successful
                        server.LastServerInfo = gameServerInfo;
                        server.LastSuccessfulPingTimestamp = DateTimeOffset.Now;
                    }

                    // TODO: fetch actual players


                    // confirm all the players that are joined after fetching the actual players
                    ConfirmJoinedPlayers(server);

                    // now do the actual check for joining
                    int nonReservedFreeSlots = gameServerInfo.FreeSlots - server.ReservedSlots;

                    // try to join as many players as slots available
                    for (int i = 0; i < nonReservedFreeSlots; i++)
                    {
                        foreach (Player player in server.PlayerQueue)
                        {
                            if (player.State is PlayerState.Joining)
                            {
                                // already joining
                                continue;
                            }

                            await TryJoinPlayer(player, server);
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    // timed out
                    server.LastServerInfo = null;
                }
                catch (Exception)
                {
                    // 
                }
            }
        }

        public bool JoinQueue(string serverIp, int serverPort, string connectionId)
        {
            if (!_connectedPlayers.TryGetValue(connectionId, out var player))
            {
                // unknown player
                return false;
            }

            if (!_servers.TryGetValue((serverIp, serverPort), out var server))
            {
                // server does not have a queue yet
                server = new("")
                {
                    ServerIp = serverIp,
                    ServerPort = serverPort
                };

                if (!_servers.TryAdd((serverIp, serverPort), server))
                {
                    // in the meantime the server got added (TODO: locking)
                    throw new Exception("invalid state");
                }

                // start processing queued players
                Task.Run(() => ProcessQueuedPlayers(server, default));
            }
            else if (server.PlayerQueue.Contains(player))
            {
                // player is already queued in server
                return false;
            }

            if (server.PlayerQueue.Count > 20)
            {
                // queue limit
                return false;
            }

            // queue the player
            server.PlayerQueue.Enqueue(player);
            player.State = PlayerState.Queued;
            player.Server = server;

            return true;
        }

        public void LeaveQueue(string connectionId)
        {
            if (!_connectedPlayers.TryGetValue(connectionId, out var player))
            {
                // unknown player
                return;
            }

            LeaveQueue(player);
        }

        private void LeaveQueue(Player player)
        {
            if (player.State is not PlayerState.Queued or PlayerState.Joining || player.Server is null)
            {
                return;
            }

            player.Server.PlayerQueue.Remove(player);

            if (player.State is PlayerState.Joining)
            {
                player.Server.ReservedSlots--;
            }
        }
    }
}
