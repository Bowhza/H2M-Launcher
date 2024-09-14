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

        [Fact]
        public void SelectMaxPlayersForMatchDesc_HappyCase()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 6 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMPlayer player4 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" },
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2, player3, player4], 1, 3)
                .Should().BeEquivalentTo([player2, player3, player4]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldRespectFreeSlots()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 6 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMPlayer player4 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" },
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2, player3, player4], joinedPlayersCount: 3, freeSlots: 2)
                .Should().BeEquivalentTo([player2, player3]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleExactMatchForFreeSlots()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2, player3], joinedPlayersCount: 2, freeSlots: 3)
                .Should().BeEquivalentTo([player1, player2, player3]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldNotSelectWhenThresholdCannotBeMet()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 10 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 8 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2], joinedPlayersCount: 1, freeSlots: 4)
                .Should().BeEmpty();
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldNotSelectWhenThresholdCannotBeMetByOne()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2], joinedPlayersCount: 1, freeSlots: 4)
                .Should().BeEmpty();
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleHighThresholdWhenEnoughPlayersAvailable()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 8 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                [],
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMPlayer player4 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" },
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2, player3, player4], joinedPlayersCount: 4, freeSlots: 8)
                .Should().BeEquivalentTo([player1, player2, player3, player4]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleExcessPlayers()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMPlayer player2 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Bob" },
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });
            MMPlayer player3 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Charlie" },
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMPlayer player4 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "David" },
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1, player2, player3, player4], joinedPlayersCount: 0, freeSlots: 2)
                .Should().BeEquivalentTo([player3, player4]); // Players with lower thresholds are selected first
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleNoFreeSlots()
        {
            MMPlayer player1 = new(
                new Player() { ConnectionId = Guid.NewGuid().ToString(), Name = "Alice" },
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });

            MatchmakingService.SelectMaxPlayersForMatchDesc([player1], joinedPlayersCount: 4, freeSlots: 0)
                .Should().BeEmpty(); // No slots, so no match
        }
    }
}