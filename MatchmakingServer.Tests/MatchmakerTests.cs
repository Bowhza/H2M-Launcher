using FluentAssertions;

using H2MLauncher.Core.Matchmaking.Models;

using MatchmakingServer.Matchmaking;
using MatchmakingServer.Matchmaking.Models;

using UniqueNamer;

namespace MatchmakingServer.Tests
{
    public class MatchmakerTests
    {
        [Fact]
        public void SelectMaxPlayersForMatchDesc_HappyCase()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 6 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMTicket ticket4 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "David" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2, ticket3, ticket4], 1, 3)
                .Should().BeEquivalentTo([ticket2, ticket3, ticket4]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_HappyCaseChunked()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" },
                 new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 6 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMTicket ticket4 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "David" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket3, ticket4], 1, 3)
                .Should().BeEquivalentTo([ticket3, ticket4]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldRespectFreeSlots()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 6 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMTicket ticket4 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "David" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2, ticket3, ticket4], joinedPlayersCount: 3, freeSlots: 2)
                .Should().BeEquivalentTo([ticket2, ticket3]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleExactMatchForFreeSlots()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2, ticket3], joinedPlayersCount: 2, freeSlots: 3)
                .Should().BeEquivalentTo([ticket1, ticket2, ticket3]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldNotSelectWhenThresholdCannotBeMet()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 10 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 8 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2], joinedPlayersCount: 1, freeSlots: 4)
                .Should().BeEmpty();
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldNotSelectWhenThresholdCannotBeMetByOne()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2], joinedPlayersCount: 1, freeSlots: 4)
                .Should().BeEmpty();
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleHighThresholdWhenEnoughPlayersAvailable()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 8 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 5 });
            MMTicket ticket4 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "David" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2, ticket3, ticket4], joinedPlayersCount: 4, freeSlots: 8)
                .Should().BeEquivalentTo([ticket1, ticket2, ticket3, ticket4]);
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleExcessPlayers()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });
            MMTicket ticket2 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Bob" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 3 });
            MMTicket ticket3 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Charlie" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 2 });
            MMTicket ticket4 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "David" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 1 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1, ticket2, ticket3, ticket4], joinedPlayersCount: 0, freeSlots: 2)
                .Should().BeEquivalentTo([ticket3, ticket4]); // Players with lower thresholds are selected first
        }

        [Fact]
        public void SelectMaxPlayersForMatchDesc_ShouldHandleNoFreeSlots()
        {
            MMTicket ticket1 = new(
                [new Player() { Id = Guid.NewGuid().ToString(), UserName = UniqueNamer.UniqueNamer.Generate([Categories.General]), Name = "Alice" }],
                [],
                new MatchSearchCriteria() { MinPlayers = 4 });

            Matchmaker.SelectMaxPlayersForMatchDesc([ticket1], joinedPlayersCount: 4, freeSlots: 0)
                .Should().BeEmpty(); // No slots, so no match
        }
    }
}