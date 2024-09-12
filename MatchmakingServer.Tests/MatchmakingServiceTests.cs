using FluentAssertions;

using H2MLauncher.Core.Models;

using static MatchmakingServer.MatchmakingService;

namespace MatchmakingServer.Tests
{
    public class MatchmakingServiceTests
    {
        [Fact]
        public void SelectLongerWaitingPlayers_IncludeJoinedPlayersInThreshold()
        {
            DateTime timeoutAgo = DateTime.Now.Subtract(TimeSpan.FromSeconds(30));

            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                servers: new() { { ("192.168.1.1", 8080), -1 }, { ("192.168.1.2", 8080), -1 } },
                new MatchSearchCriteria() { MinPlayers = 2 })
            { JoinTime = timeoutAgo };
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                servers: new() { { ("192.168.1.1", 8080), -1 }, { ("192.168.1.3", 8080), -1 } },
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                servers: new() { { ("192.168.1.1", 8080), -1 } }, 
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMPlayer player4 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" },
                servers: new() { { ("192.168.1.2", 8080), -1 } }, 
                new MatchSearchCriteria() { MinPlayers = 5 });

            MatchmakingService.SelectMaxPlayersForMatchWithTimeout([player1, player2, player3, player4], 1, 5)
                .Should().BeEmpty();
        }
    }
}