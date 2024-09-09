using FluentAssertions;

using static MatchmakingServer.MatchmakingService;

namespace MatchmakingServer.Tests
{
    public class MatchmakingServiceTests
    {
        [Fact]
        public void SelectLongerWaitingPlayers_IncludeJoinedPlayersInThreshold()
        {
            DateTime timeoutAgo = DateTime.Now.Subtract(TimeSpan.FromSeconds(30));            
            
            MMPlayer player1 = new(new() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice"}, [("192.168.1.1", 8080), ("192.168.1.2", 8080)], 2) { JoinTime = timeoutAgo };
            MMPlayer player2 = new(new() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" }, [("192.168.1.1", 8080), ("192.168.1.3",8080)], 5);
            MMPlayer player3 = new(new() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" }, [("192.168.1.1", 8080)], 5);
            MMPlayer player4 = new(new() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" }, [("192.168.1.2", 8080)], 5);            

            MatchmakingService.SelectMaxPlayersForMatchWithTimeout([player1, player2, player3, player4], 1, 5)
                .Should().BeEquivalentTo([player1]);
        }
    }
}