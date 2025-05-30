using FluentAssertions;

using MatchmakingServer.Core.Social;
using MatchmakingServer.Database;
using MatchmakingServer.Database.Entities;
using MatchmakingServer.Parties;
using MatchmakingServer.SignalR;

namespace MatchmakingServer.Tests.IntegrationTests;

public class FriendshipControllerTests(Factory factory) : IClassFixture<Factory>, IAsyncLifetime
{
    // This will hold the scoped DbContext for the current test
    private DatabaseContext _dbContext = null!;
    private PlayerStore _playerStore = null!;

    public async Task InitializeAsync()
    {
        var scope = factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        _playerStore = scope.ServiceProvider.GetRequiredService<PlayerStore>();

        // Clean the database for this test
        await ResetUsersAndFriendships(_dbContext);
    }

    public async Task DisposeAsync()
    {
        // Dispose the DbContext and its scope
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
            await _playerStore.Clear();
        }
    }

    #region GetFriends

    [Fact]
    public async Task GetFriends_ForUserWithNoFriends_ReturnsEmptyList()
    {
        // Arrange
        UserDbo user = CreateUser(lastPlayerName: "last-player-name");

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(user.Id, user.Name);
        FriendDto[]? friends = await client.GetFromJsonAsync<FriendDto[]>(
            $"/users/{user.Id}/friends", JsonSerialization.SerializerOptions);

        // Assert
        friends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriends_ReturnsOnlyAcceptedFriendsForUser_WithCurrentStatus()
    {
        // Arrange
        UserDbo user = CreateUser(lastPlayerName: "last-player-name");
        UserDbo friend1 = CreateUser(lastPlayerName: "friend-1-player-name");
        UserDbo friend2 = CreateUser();
        UserDbo notAFriend = CreateUser();
        UserDbo notAFriend2 = CreateUser();
        UserDbo pendingFriend = CreateUser();
        UserDbo rejectedFriend = CreateUser();

        FriendshipDbo friendship1 = CreateAcceptedFriendship(user, friend1);
        FriendshipDbo friendship2 = CreateAcceptedFriendship(friend2, user);
        FriendshipDbo pendingFriendship = CreatePendingFriendship(pendingFriend, user);
        FriendshipDbo rejectedFriendship = CreateRejectedFriendship(rejectedFriend, user);
        FriendshipDbo notAFriendship = CreateAcceptedFriendship(notAFriend, notAFriend2); // Friendship not involving 'user'


        _dbContext.Users.Add(user);
        _dbContext.Users.Add(friend1);
        _dbContext.Users.Add(friend2);
        _dbContext.Users.Add(notAFriend);
        _dbContext.Users.Add(notAFriend2);
        _dbContext.Users.Add(pendingFriend);
        _dbContext.Users.Add(rejectedFriend);

        _dbContext.UserFriendships.Add(friendship1);
        _dbContext.UserFriendships.Add(friendship2);
        _dbContext.UserFriendships.Add(pendingFriendship);
        _dbContext.UserFriendships.Add(rejectedFriendship);
        _dbContext.UserFriendships.Add(notAFriendship);

        await _dbContext.SaveChangesAsync();



        // Create a social hub session for that player
        Player friend2Player = await _playerStore.GetOrAdd(
            friend2.Id.ToString(), friend2.Name, friend2.Id.ToString(), "friend-2-current-player-name");
        friend2Player.SocialHubId = "some-id";
        friend2Player.GameStatus = GameStatus.InLobby;

        Player notAFriendPlayer = await _playerStore.GetOrAdd(
            notAFriend.Id.ToString(), notAFriend.Name, notAFriend.Id.ToString(), "not-a-friend-current-player-name");

        Party friend2Party = new(friend2Player);
        friend2Party.AddPlayer(friend2Player);
        friend2Party.AddInvite(notAFriendPlayer, DateTime.UtcNow.AddMinutes(10));

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(user.Id, user.Name);
        FriendDto[]? friends = await client.GetFromJsonAsync<FriendDto[]>($"/users/{user.Id}/friends", JsonSerialization.SerializerOptions);

        // Assert
        friends.Should().HaveCount(2);
        friends.Should().BeEquivalentTo([
            new FriendDto(
                friend1.Id.ToString(),
                friend1.Name,
                friend1.LastPlayerName,
                OnlineStatus.Offline,
                GameStatus.None,
                null,
                friendship1.UpdateDate
            ),
            new FriendDto(
                friend2.Id.ToString(),
                friend2.Name,
                "friend-2-current-player-name",
                OnlineStatus.Online,
                GameStatus.InLobby,
                new PartyStatusDto(friend2Party.Id, 1, true, [notAFriend.Id.ToString()]),
                friendship2.UpdateDate
            )
        ], options => options.WithoutStrictOrdering());
    }

    #endregion


    #region GetFriendRequests

    [Fact]
    public async Task GetFriendRequests_ReturnsOnlyPendingFriends()
    {
        // Arrange
        UserDbo user = CreateUser(lastPlayerName: "last-player-name");
        UserDbo friend1 = CreateUser(lastPlayerName: "friend-1-player-name");
        UserDbo friend2 = CreateUser();
        UserDbo notAFriend = CreateUser();
        UserDbo notAFriend2 = CreateUser();
        UserDbo pendingFriend = CreateUser();
        UserDbo rejectedFriend = CreateUser();

        FriendshipDbo friendship1 = CreateAcceptedFriendship(user, friend1);
        FriendshipDbo friendship2 = CreateAcceptedFriendship(friend2, user);
        FriendshipDbo pendingFriendship = CreatePendingFriendship(pendingFriend, user);
        FriendshipDbo rejectedFriendship = CreateRejectedFriendship(rejectedFriend, user);
        FriendshipDbo notAFriendship = CreateAcceptedFriendship(notAFriend, notAFriend2); // Friendship not involving 'user'


        _dbContext.Users.Add(user);
        _dbContext.Users.Add(friend1);
        _dbContext.Users.Add(friend2);
        _dbContext.Users.Add(notAFriend);
        _dbContext.Users.Add(notAFriend2);
        _dbContext.Users.Add(pendingFriend);
        _dbContext.Users.Add(rejectedFriend);

        _dbContext.UserFriendships.Add(friendship1);
        _dbContext.UserFriendships.Add(friendship2);
        _dbContext.UserFriendships.Add(pendingFriendship);
        _dbContext.UserFriendships.Add(rejectedFriendship);
        _dbContext.UserFriendships.Add(notAFriendship);

        await _dbContext.SaveChangesAsync();


        // Act
        HttpClient client = factory.CreateAuthenticatedClient(user.Id, user.Name);
        FriendDto[]? friends = await client.GetFromJsonAsync<FriendDto[]>($"/users/{user.Id}/friends", JsonSerialization.SerializerOptions);

        // Assert
        friends.Should().HaveCount(2);
        friends.Should().Contain([
            new FriendDto(
                friend1.Id.ToString(),
                friend1.Name,
                friend1.LastPlayerName,
                OnlineStatus.Offline,
                GameStatus.None,
                null,
                friendship1.UpdateDate
            ),
            new FriendDto(
                friend2.Id.ToString(),
                friend2.Name,
                friend2.LastPlayerName,
                OnlineStatus.Offline,
                GameStatus.None,
                null,
                friendship2.UpdateDate
            )
        ]);
    }

    #endregion


    #region GetFriendRequest

    [Theory]
    [InlineData(FriendshipStatus.Pending, true, true, FriendRequestStatus.PendingOutgoing)] // Outgoing Pending
    [InlineData(FriendshipStatus.Pending, false, true, FriendRequestStatus.PendingIncoming)] // Incoming Pending
    [InlineData(FriendshipStatus.Rejected, true, true, FriendRequestStatus.PendingOutgoing)] // Outgoing Rejected
    [InlineData(FriendshipStatus.Rejected, false, false, null)] // Incoming Rejected (NOT Visible)
    [InlineData(FriendshipStatus.Accepted, true, false, null)] // Outgoing Accepted (NOT Visible)
    [InlineData(FriendshipStatus.Accepted, false, false, null)] // Incoming Accepted (NOT Visible)
    public async Task GetFriendRequest_VariousFriendshipStates_ReturnsCorrectVisibility(
        FriendshipStatus statusToTest,
        bool isOutgoing,
        bool expectRequestFound,
        FriendRequestStatus? expectedDirection
    )
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo otherUser = CreateUser(lastPlayerName: "last-player-name");

        FriendshipDbo friendship;
        if (isOutgoing)
        {
            friendship = CreateFriendship(requestingUser, otherUser, statusToTest);
        }
        else
        {
            friendship = CreateFriendship(otherUser, requestingUser, statusToTest);
        }


        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(otherUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.GetAsync($"/users/{requestingUser.Id}/friend-requests/{otherUser.Id}");

        // Assert
        if (expectRequestFound)
        {
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            FriendRequestDto? result = await response.Content.ReadFromJsonAsync<FriendRequestDto>(JsonSerialization.SerializerOptions);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new FriendRequestDto()
            {
                UserId = otherUser.Id,
                UserName = otherUser.Name,
                PlayerName = otherUser.LastPlayerName,
                Status = expectedDirection!.Value,
                Created = DateTime.SpecifyKind(friendship.CreationDate, DateTimeKind.Local),
            }, options => options.ComparingRecordsByValue());
        }
        else
        {
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
    }


    [Fact]
    public async Task GetFriendRequest_NoFriendshipExists_ReturnsNotFound()
    {
        // Arrange
        UserDbo user1 = CreateUser();
        UserDbo user2 = CreateUser();

        _dbContext.Users.Add(user1);
        _dbContext.Users.Add(user2);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(user1.Id, user1.Name);
        HttpResponseMessage response = await client.GetAsync($"/users/{user1.Id}/friend-requests/{user2.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task GetFriendRequest_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo nonExistentUser = CreateUser(); // This user ID will be in the URL but not in DB
        UserDbo targetUser = CreateUser();

        _dbContext.Users.Add(targetUser);
        await _dbContext.SaveChangesAsync();


        // Act
        HttpClient client = factory.CreateAuthenticatedClient(nonExistentUser.Id, nonExistentUser.Name);
        HttpResponseMessage response = await client.GetAsync($"/users/{nonExistentUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFriendRequest_TargetUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo nonExistentTargetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.GetAsync($"/users/{requestingUser.Id}/friend-requests/{nonExistentTargetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    #endregion


    #region SendFriendRequest

    [Fact]
    public async Task SendFriendRequest_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(targetUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendFriendRequest_TargetUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo nonExistentTargetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(nonExistentTargetUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendFriendRequest_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(requestingUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendFriendRequest_FromOtherUser_ReturnsForbidden(bool otherUserExists)
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo fromOtherUser = CreateUser();
        UserDbo targetUser = CreateUser();

        if (otherUserExists)
        {
            await _dbContext.Users.AddAsync(fromOtherUser);
        }

        await _dbContext.Users.AddRangeAsync(requestingUser, targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{fromOtherUser.Id}/friend-requests", new SendFriendRequestDto(targetUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(FriendshipStatus.Pending, true)] // Outgoing Pending
    [InlineData(FriendshipStatus.Pending, false)] // Incoming Pending
    [InlineData(FriendshipStatus.Rejected, true)] // Outgoing Rejected
    [InlineData(FriendshipStatus.Accepted, true)] // Outgoing Accepted
    [InlineData(FriendshipStatus.Accepted, false)] // Incoming Accepted
    public async Task SendFriendRequest_WithExistingFriendRequests_ReturnsConflict(
        FriendshipStatus statusToTest, bool isOutgoing)
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo otherUser = CreateUser(lastPlayerName: "last-player-name");

        FriendshipDbo friendship;
        if (isOutgoing)
        {
            friendship = CreateFriendship(requestingUser, otherUser, statusToTest);
        }
        else
        {
            friendship = CreateFriendship(otherUser, requestingUser, statusToTest);
        }

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(otherUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(otherUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SendFriendRequest_WhenPreviouslyRejectedIncoming_ReturnsCreatedRequest()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo otherUser = CreateUser(lastPlayerName: "last-player-name");

        FriendshipDbo friendship = CreateRejectedFriendship(otherUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(otherUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(otherUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        FriendRequestDto? result = await response.Content.ReadFromJsonAsync<FriendRequestDto>(JsonSerialization.SerializerOptions);
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new FriendRequestDto()
        {
            UserId = otherUser.Id,
            UserName = otherUser.Name,
            PlayerName = otherUser.LastPlayerName,
            Status = FriendRequestStatus.PendingOutgoing,
        }, options => options.Excluding(r => r.Created));
        result!.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SendFriendRequest_NoExistingFriendship_ReturnsCreatedRequest()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo otherUser = CreateUser(lastPlayerName: "last-player-name");

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(otherUser);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/users/{requestingUser.Id}/friend-requests", new SendFriendRequestDto(otherUser.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        FriendRequestDto? result = await response.Content.ReadFromJsonAsync<FriendRequestDto>(JsonSerialization.SerializerOptions);
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new FriendRequestDto()
        {
            UserId = otherUser.Id,
            UserName = otherUser.Name,
            PlayerName = otherUser.LastPlayerName,
            Status = FriendRequestStatus.PendingOutgoing,
        }, options => options.Excluding(r => r.Created));
        result!.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    #endregion


    #region AcceptFriendRequest

    [Fact]
    public async Task AcceptFriendRequest_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser(); // This user ID will be in the URL but not in DB
        UserDbo targetUser = CreateUser();

        _dbContext.Users.Add(targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptFriendRequest_TargetUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptFriendRequest_NoRequestDoesExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        await _dbContext.Users.AddRangeAsync(requestingUser, targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptFriendRequest_WhenOutgoingPending_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreatePendingFriendship(requestingUser, targetUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptFriendRequest_WhenAlreadyFriends_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreateAcceptedFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptFriendRequest_WhenAlreadyRejected_ReturnsConflict()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreateRejectedFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AcceptFriendRequest_PendingIncoming_ReturnsCreatedFriend()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo pendingFriendship = CreatePendingFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(pendingFriendship);

        await _dbContext.SaveChangesAsync();

        FriendDto expectedFriendDto = new(
            targetUser.Id.ToString(),
            targetUser.Name,
            targetUser.LastPlayerName,
            OnlineStatus.Offline,
            GameStatus.None,
            null,
            DateTime.UtcNow
        );

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.PutAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}", null);
        FriendDto? acceptedFriend = await response.Content.ReadFromJsonAsync<FriendDto>(JsonSerialization.SerializerOptions);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        acceptedFriend.Should().BeEquivalentTo(expectedFriendDto, options => options.Excluding(f => f.FriendsSince));
        acceptedFriend!.FriendsSince.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));

        FriendDto? friend = await client.GetFromJsonAsync<FriendDto>(
            $"/users/{requestingUser.Id}/friends/{targetUser.Id}", JsonSerialization.SerializerOptions);

        friend.Should().BeEquivalentTo(expectedFriendDto, options => options.Excluding(f => f.FriendsSince));
    }

    #endregion


    #region RejectFriendRequest

    [Fact]
    public async Task RejectFriendRequest_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser(); // This user ID will be in the URL but not in DB
        UserDbo targetUser = CreateUser();

        _dbContext.Users.Add(targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectFriendRequest_TargetUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectFriendRequest_NoRequestDoesExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        await _dbContext.Users.AddRangeAsync(requestingUser, targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectFriendRequest_WhenOutgoingPending_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreatePendingFriendship(requestingUser, targetUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectFriendRequest_WhenAlreadyFriends_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreateAcceptedFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectFriendRequest_WhenAlreadyRejected_ReturnsConflict()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo friendship = CreateRejectedFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(friendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RejectFriendRequest_PendingIncoming_ReturnsNoContent()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo pendingFriendship = CreatePendingFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(pendingFriendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friend-requests/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    #endregion


    #region GetAllFriends

    [Theory]
    [MemberData(nameof(GetAllFriendRequestsData))]
    public async Task GetAllFriendRequests_ReturnsCorrectRequestsForScenario(
        UserDbo user,
        List<FriendshipDbo> friendshipsToSetup,
        List<FriendRequestDto> expectedFriendRequests)
    {
        // Arrange

        // Add the main user
        _dbContext.Users.Add(user);

        // Add all friendships for the scenario
        _dbContext.UserFriendships.AddRange(friendshipsToSetup);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(user.Id, user.Name);
        FriendRequestDto[]? friendRequests = await client.GetFromJsonAsync<FriendRequestDto[]>(
            $"/users/{user.Id}/friend-requests", JsonSerialization.SerializerOptions);

        // Assert
        friendRequests.Should().NotBeNull();
        friendRequests.Should().HaveCount(expectedFriendRequests.Count);
        friendRequests.Should().BeEquivalentTo(expectedFriendRequests,
            options => options.ComparingRecordsByValue().WithoutStrictOrdering());
    }

    [Fact]
    public async Task GetAllFriendRequests_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo nonExistentUser = CreateUser(); // This user ID will be in the URL but not in DB

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(nonExistentUser.Id, nonExistentUser.Name);
        HttpResponseMessage response = await client.GetAsync($"/users/{nonExistentUser.Id}/friend-requests");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    public static TheoryData<UserDbo, List<FriendshipDbo>, List<FriendRequestDto>> GetAllFriendRequestsData
    {
        get
        {
            TheoryData<UserDbo, List<FriendshipDbo>, List<FriendRequestDto>> data = [];

            // Scenario 1: User with a mix of all relevant and irrelevant friendship states
            UserDbo user1 = CreateUser(name: "User1");
            UserDbo pendingIncomingFriend = CreateUser(name: "PendingIncoming");
            UserDbo pendingOutgoingFriend = CreateUser(name: "PendingOutgoing");
            UserDbo rejectedOutgoingFriend = CreateUser(name: "RejectedOutgoing");
            UserDbo rejectedIncomingFriend = CreateUser(name: "RejectedIncoming"); // Should NOT be returned
            UserDbo acceptedFriend = CreateUser(name: "AcceptedFriend"); // Should NOT be returned
            UserDbo unrelatedUser = CreateUser(name: "UnrelatedUser"); // Should NOT be returned

            FriendshipDbo friendship1_pendingIncoming = CreatePendingFriendship(pendingIncomingFriend, user1);
            FriendshipDbo friendship1_pendingOutgoing = CreatePendingFriendship(user1, pendingOutgoingFriend);
            FriendshipDbo friendship1_rejectedOutgoing = CreateRejectedFriendship(user1, rejectedOutgoingFriend);
            FriendshipDbo friendship1_rejectedIncoming = CreateRejectedFriendship(rejectedIncomingFriend, user1);
            FriendshipDbo friendship1_accepted = CreateAcceptedFriendship(user1, acceptedFriend);
            FriendshipDbo friendship1_unrelated = CreateAcceptedFriendship(unrelatedUser, CreateUser()); // Completely unrelated

            data.Add(user1,
            [
                friendship1_pendingIncoming,
                friendship1_pendingOutgoing,
                friendship1_rejectedOutgoing,
                friendship1_rejectedIncoming, // This will be set up but not expected in results
                friendship1_accepted,         // This will be set up but not expected in results
                friendship1_unrelated         // This will be set up but not expected in results
            ],
            [
                new FriendRequestDto() {
                    UserId = pendingIncomingFriend.Id,
                    UserName = pendingIncomingFriend.Name,
                    Status = FriendRequestStatus.PendingIncoming,
                    Created = DateTime.SpecifyKind(friendship1_pendingIncoming.CreationDate, DateTimeKind.Local)
                },
                new FriendRequestDto() {
                    UserId = pendingOutgoingFriend.Id,
                    UserName = pendingOutgoingFriend.Name,
                    Status = FriendRequestStatus.PendingOutgoing,
                    Created = DateTime.SpecifyKind(friendship1_pendingOutgoing.CreationDate, DateTimeKind.Local)
                },
                new FriendRequestDto()
                {
                    UserId = rejectedOutgoingFriend.Id,
                    UserName = rejectedOutgoingFriend.Name,
                    Status = FriendRequestStatus.PendingOutgoing,
                    Created = DateTime.SpecifyKind(friendship1_rejectedOutgoing.UpdateDate, DateTimeKind.Local)
                }
            ]);

            // Scenario 2: User has no friend requests at all
            var user2 = CreateUser(name: "User2");
            data.Add(user2,
                [], // No friendships
                []); // Empty list expected

            // Scenario 3: User has only incoming rejected requests (should return empty)
            UserDbo user3 = CreateUser(name: "User3");
            UserDbo rejectedIncomingFriend3_1 = CreateUser(name: "RejectedIncoming1");
            UserDbo rejectedIncomingFriend3_2 = CreateUser(name: "RejectedIncoming2");

            FriendshipDbo friendship3_rejectedIncoming1 = CreateRejectedFriendship(rejectedIncomingFriend3_1, user3);
            FriendshipDbo friendship3_rejectedIncoming2 = CreateRejectedFriendship(rejectedIncomingFriend3_2, user3);

            data.Add(user3,
                [
                    friendship3_rejectedIncoming1,
                    friendship3_rejectedIncoming2
                ],
                []); // Empty list expected

            // Scenario 4: User has only accepted friendships (should return empty)
            UserDbo user4 = CreateUser(name: "User4");
            UserDbo acceptedFriend4_1 = CreateUser(name: "Accepted1");
            UserDbo acceptedFriend4_2 = CreateUser(name: "Accepted2");

            FriendshipDbo friendship4_accepted1 = CreateAcceptedFriendship(user4, acceptedFriend4_1);
            FriendshipDbo friendship4_accepted2 = CreateAcceptedFriendship(acceptedFriend4_2, user4);

            data.Add(user4,
                [
                    friendship4_accepted1,
                    friendship4_accepted2
                ],
                []); // Empty list expected

            return data;
        }
    }

    #endregion


    #region Unfriend

    [Fact]
    public async Task Unfriend_RequestingUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser(); // This user ID will be in the URL but not in DB
        UserDbo targetUser = CreateUser();

        _dbContext.Users.Add(targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfriend_TargetUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser(); // This user ID will be in the URL but not in DB

        _dbContext.Users.Add(requestingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfriend_NoFriendship_ReturnsNotFound()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        await _dbContext.Users.AddRangeAsync(requestingUser, targetUser);
        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfriend_ExistingFriendship_ReturnsNoContent()
    {
        // Arrange
        UserDbo requestingUser = CreateUser();
        UserDbo targetUser = CreateUser();

        FriendshipDbo pendingFriendship = CreateAcceptedFriendship(targetUser, requestingUser);

        _dbContext.Users.Add(requestingUser);
        _dbContext.Users.Add(targetUser);
        _dbContext.UserFriendships.Add(pendingFriendship);

        await _dbContext.SaveChangesAsync();

        // Act
        HttpClient client = factory.CreateAuthenticatedClient(requestingUser.Id, requestingUser.Name);
        HttpResponseMessage response = await client.DeleteAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        HttpResponseMessage friendResponse = await client.GetAsync($"/users/{requestingUser.Id}/friends/{targetUser.Id}");
        friendResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    #endregion

    private static Task<int> ResetUsersAndFriendships(DatabaseContext dbContext)
    {
        dbContext.Users.RemoveRange(dbContext.Users);
        dbContext.UserFriendships.RemoveRange(dbContext.UserFriendships);
        return dbContext.SaveChangesAsync();
    }

    private static UserDbo CreateUser(string? name = null, string? lastPlayerName = null)
    {
        return new UserDbo
        {
            Id = Guid.NewGuid(),
            Name = name ?? UniqueNamer.UniqueNamer.Generate([UniqueNamer.Categories.General]),
            LastPlayerName = lastPlayerName
        };
    }

    private static FriendshipDbo CreateFriendship(UserDbo fromUser, UserDbo toUser, FriendshipStatus status, DateTime? updateDate = null)
    {
        return new FriendshipDbo
        {
            FromUser = fromUser,
            ToUser = toUser,
            Status = status,
            UpdateDate = new DateTime(((updateDate ?? DateTime.UtcNow).Ticks / 100) * 100),
            CreationDate = new DateTime((DateTime.UtcNow.Ticks / 100) * 100),
        };
    }

    // Helper for common friendships
    private static FriendshipDbo CreateAcceptedFriendship(UserDbo fromUser, UserDbo toUser, DateTime? updateDate = null)
        => CreateFriendship(fromUser, toUser, FriendshipStatus.Accepted, updateDate);

    private static FriendshipDbo CreatePendingFriendship(UserDbo fromUser, UserDbo toUser)
        => CreateFriendship(fromUser, toUser, FriendshipStatus.Pending);

    private static FriendshipDbo CreateRejectedFriendship(UserDbo fromUser, UserDbo toUser, DateTime? updateDate = null)
        => CreateFriendship(fromUser, toUser, FriendshipStatus.Rejected, updateDate);
}
