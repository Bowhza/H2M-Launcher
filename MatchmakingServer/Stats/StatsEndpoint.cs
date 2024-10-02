using MatchmakingServer.Api;
using MatchmakingServer.Matchmaking;
using MatchmakingServer.Parties;
using MatchmakingServer.SignalR;

namespace MatchmakingServer.Stats;

public class StatsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) => app
        .MapGet("/stats", (PlayerStore playerStore, ServerStore serverStore, PartyService partyService, Matchmaker matchmaker) =>
        {
            return new
            {
                ConnectedPlayers = playerStore.NumConnectedPlayers,
                TotalPlayersSeen = playerStore.NumPlayersSeen,
                TotalPlayersSeenToday = playerStore.NumPlayersSeenToday,
                QueuedServers = serverStore.Servers.Where(s => s.Value.ProcessingState is QueueProcessingState.Running).Count(),
                QueuedPlayers = serverStore.Servers.Values.Sum(s => s.PlayerQueue.Count),
                MatchmakingTickets = matchmaker.Tickets.Count,
                MatchmakingPlayers = matchmaker.Tickets.Sum(t => t.Players.Count),
                MatchmakingServers = matchmaker.QueuedServers.Count,
                Parties = partyService.Parties.Count,
                PartyMembers = partyService.Parties.Sum(p => p.Members.Count)
            };
        });
}
