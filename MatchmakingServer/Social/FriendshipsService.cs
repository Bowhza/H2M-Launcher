using FxKit;

using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;

using MatchmakingServer.Database;
using MatchmakingServer.Database.Entities;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingServer.Social;

public sealed class FriendshipsService(
    DatabaseContext dbContext,
    PlayerStore playerStore,
    IHubContext<SocialHub, ISocialClient> socialHubContext,
    ILogger<FriendshipsService> logger)
{
    private readonly DatabaseContext _dbContext = dbContext;
    private readonly PlayerStore _playerStore = playerStore;
    private readonly IHubContext<SocialHub, ISocialClient> _socialHubContext = socialHubContext;
    private readonly ILogger<FriendshipsService> _logger = logger;

    public Task<List<Guid>> GetFriendIdsAsync(Guid userId)
    {
        return _dbContext.UserFriendships
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Where(r => r.Status == FriendshipStatus.Accepted &&
                        (r.FromUserId == userId || r.ToUserId == userId))
            .Select(r => r.ToUserId == userId ? r.FromUserId : r.ToUserId)
            .ToListAsync();
    }

    private Task<Dictionary<UserDbo, FriendshipDbo>> GetFriendRelationsAsync(Guid userId, FriendshipStatus[] status, CancellationToken cancellationToken)
    {
        return _dbContext.UserFriendships
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Where(r => status.Contains(r.Status) &&
                        (r.FromUserId == userId || r.ToUserId == userId))
            .ToDictionaryAsync(r => r.GetFriendByUserId(userId)!, cancellationToken);
    }

    public async Task<Result<List<FriendDto>, FriendshipError>> GetFriendsWithStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            List<FriendDto> friends = [];

            if (!await _dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            {
                return Err<List<FriendDto>, FriendshipError>(FriendshipError.UserNotFound);
            }

            Dictionary<UserDbo, FriendshipDbo> friendRelations = await GetFriendRelationsAsync(
                userId, [FriendshipStatus.Accepted], cancellationToken);

            foreach ((UserDbo friend, FriendshipDbo relationship) in friendRelations)
            {
                friends.Add(await CreateFriendWithStatus(friend, relationship.UpdateDate));
            }

            return friends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting friends for {userId}", userId);
            return Err<List<FriendDto>, FriendshipError>(FriendshipError.UnknownError);
        }
    }

    public async Task<Result<FriendDto, FriendshipError>> GetFriendWithStatusAsync(Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        try
        {
            List<FriendDto> friends = [];

            if (!await _dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            {
                return Err<FriendDto, FriendshipError>(FriendshipError.UserNotFound);
            }

            FriendshipDbo? relationship = await GetFriendshipQuery(userId, friendId)
                .Where(r => r.Status == FriendshipStatus.Accepted)
                .Include(r => r.FromUser)
                .Include(r => r.ToUser)
                .FirstOrDefaultAsync(cancellationToken);

            if (relationship is null)
            {
                return Err<FriendDto, FriendshipError>(FriendshipError.UserNotFound);
            }

            UserDbo friend = relationship.GetUserById(friendId)!;

            return await CreateFriendWithStatus(friend, relationship.UpdateDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting friends for {userId}", userId);
            return Err<FriendDto, FriendshipError>(FriendshipError.UnknownError);
        }
    }

    private async Task<FriendDto> CreateFriendWithStatus(UserDbo friend, DateTime friendsSince)
    {
        Player? player = await _playerStore.TryGet(friend.Id.ToString());

        if (player is null || player.SocialHubId is null)
        {
            return new FriendDto(
                friend.Id.ToString(),
                friend.Name,
                friend.LastPlayerName,
                OnlineStatus.Offline,
                GameStatus.None,
                null,
                friendsSince);
        }
        else
        {
            return new FriendDto(
                friend.Id.ToString(),
                friend.Name,
                player.Name,
                OnlineStatus.Online,
                player.GameStatus,
                player.Party is not null
                        ? new PartyStatusDto(
                            player.Party.Id,
                            player.Party.Members.Count,
                            player.Party.Privacy is not PartyPrivacy.Closed,
                            player.Party.ValidInvites.ToList())
                        : null,
                friendsSince);
        }
    }

    private IQueryable<FriendshipDbo> GetFriendshipQuery(Guid userId1, Guid userId2)
    {
        return _dbContext.UserFriendships
            .Where(r => r.FromUserId == userId1 && r.ToUserId == userId2 ||
                        r.FromUserId == userId2 && r.ToUserId == userId1);
    }

    public async Task<Option<FriendRequestDto>> GetFriendRequest(Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        List<FriendRequestDto> friendRequests = [];

        FriendshipDbo? relationship = await GetFriendshipQuery(userId, friendId)
            .Where(r => r.Status == FriendshipStatus.Pending ||
                        r.Status == FriendshipStatus.Rejected)
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .FirstOrDefaultAsync(cancellationToken);

        if (relationship is null)
        {
            return None;
        }

        UserDbo friend = relationship.GetUserById(friendId)!;

        return CreateFriendRequest(relationship, friend);
    }

    public async Task<Result<List<FriendRequestDto>, FriendshipError>> GetFriendRequests(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            List<FriendRequestDto> friendRequests = [];

            if (!await _dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            {
                return Err<List<FriendRequestDto>, FriendshipError>(FriendshipError.UserNotFound);
            }

            Dictionary<UserDbo, FriendshipDbo> pendingRelationships = await GetFriendRelationsAsync(
                userId, [FriendshipStatus.Pending, FriendshipStatus.Rejected], cancellationToken);

            foreach ((UserDbo friend, FriendshipDbo relationship) in pendingRelationships)
            {
                Option<FriendRequestDto> friendRequest = CreateFriendRequest(relationship, friend);

                if (friendRequest.IsSome)
                {
                    friendRequests.Add(friendRequest.Unwrap());
                }
            }

            return friendRequests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting friend requests for {userId}", userId);
            return Err<List<FriendRequestDto>, FriendshipError>(FriendshipError.UnknownError);
        }
    }

    private static Option<FriendRequestDto> CreateFriendRequest(FriendshipDbo relationship, UserDbo friend)
    {
        if (relationship.Status is FriendshipStatus.Pending)
        {
            return new FriendRequestDto()
            {
                UserId = friend.Id,
                Status = friend.Id == relationship.FromUserId
                    ? FriendRequestStatus.PendingIncoming
                    : FriendRequestStatus.PendingOutgoing,
                UserName = friend.Name,
                PlayerName = friend.LastPlayerName,
                Created = relationship.CreationDate,
            };
        }
        else if (relationship.Status is FriendshipStatus.Rejected && friend.Id == relationship.ToUserId) // outgoing rejected request
        {
            // show to user as still pending
            return new FriendRequestDto()
            {
                UserId = friend.Id,
                UserName = friend.Name,
                PlayerName = friend.LastPlayerName,
                Status = FriendRequestStatus.PendingOutgoing,
                Created = relationship.CreationDate,
            };
        }
        else
        {
            return None;
        }
    }


    public Task<bool> UsersAreFriends(Guid userId1, Guid userId2)
    {
        return _dbContext.UserFriendships.AnyAsync(r =>
            r.Status == FriendshipStatus.Accepted &&
            (r.FromUserId == userId1 && r.ToUserId == userId2 ||
            r.FromUserId == userId2 && r.ToUserId == userId1));
    }

    public async Task<bool> CanUserViewFriendsOf(Guid requestingUserId, Guid targetUserId, Guid friendId = default)
    {
        bool usersAreFriends = await UsersAreFriends(requestingUserId, targetUserId);
        if (!usersAreFriends)
        {
            return false;
        }

        if (friendId == default)
        {
            return true;
        }

        // Requested friend is accepted by target user
        return await UsersAreFriends(targetUserId, friendId);
    }

    public async Task<Result<FriendRequestDto, FriendshipError>> SendFriendRequest(
        Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        try
        {
            if (fromUserId == toUserId)
            {
                return Err<FriendRequestDto, FriendshipError>(FriendshipError.RequestToYourself);
            }

            var fromUser = await _dbContext.Users
                .Where(u => u.Id == fromUserId)
                .Select(u => new { u.Name, u.LastPlayerName })
                .FirstOrDefaultAsync(cancellationToken);

            if (fromUser is null)
            {
                return Err<FriendRequestDto, FriendshipError>(FriendshipError.UserNotFound);
            }

            UserDbo? targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == toUserId, cancellationToken);
            if (targetUser is null)
            {
                return Err<FriendRequestDto, FriendshipError>(FriendshipError.UserNotFound);
            }

            FriendshipDbo? existingRelationship = await _dbContext.UserFriendships
                .FirstOrDefaultAsync(r =>
                    r.FromUserId == fromUserId && r.ToUserId == toUserId ||
                    r.FromUserId == toUserId && r.ToUserId == fromUserId, cancellationToken);

            if (existingRelationship is not null)
            {
                if (existingRelationship.Status is FriendshipStatus.Accepted)
                {
                    return Err<FriendRequestDto, FriendshipError>(FriendshipError.AlreadyFriends);
                }

                if (existingRelationship.Status is FriendshipStatus.Pending)
                {
                    return Err<FriendRequestDto, FriendshipError>(FriendshipError.RequestPending);
                }

                if (existingRelationship.Status is FriendshipStatus.Rejected)
                {
                    if (existingRelationship.FromUserId == fromUserId)
                    {
                        // Target user has rejected senders request before
                        return Err<FriendRequestDto, FriendshipError>(FriendshipError.AlreadyRejected);
                    }

                    // Requesting user has rejected before -> turn into pending friendship


                    // NOTE: we have to reverse the directions, because previously the request was from the other person.
                    // This can only be done by remove the old relationship and storing a new one.
                    // We might want to change this behavior to automatically accept in this case in the future, 
                    // since rejected requests are still displayed as pending for the sender.
                    _dbContext.UserFriendships.Remove(existingRelationship);
                }
            }

            FriendshipDbo relationship = new()
            {
                FromUserId = fromUserId,
                ToUserId = toUserId,
                Status = FriendshipStatus.Pending,
                CreationDate = DateTime.UtcNow,
                UpdateDate = DateTime.UtcNow,
            };

            await _dbContext.UserFriendships.AddAsync(relationship, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Notify the recipient of the friend request
            await _socialHubContext.Clients
                .User(toUserId.ToString())
                .OnFriendRequestReceived(new FriendRequestDto()
                {
                    UserId = fromUserId,
                    UserName = fromUser.Name,
                    PlayerName = fromUser.LastPlayerName,
                    Status = FriendRequestStatus.PendingIncoming,
                    Created = relationship.CreationDate
                });

            return CreateFriendRequest(relationship, targetUser)
                .OkOr(FriendshipError.UnknownError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sennding friend request from {userId} to {toUserId}", fromUserId, toUserId);
            return Err<FriendRequestDto, FriendshipError>(FriendshipError.UnknownError);
        }
    }

    public async Task<Result<FriendDto, FriendshipError>> AcceptFriendRequest(
        Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        try
        {
            FriendshipDbo? existingRelationship = await _dbContext.UserFriendships
                .Include(ur => ur.FromUser)
                .Include(ur => ur.ToUser)
                .FirstOrDefaultAsync(r =>
                    r.FromUserId == fromUserId && r.ToUserId == toUserId, cancellationToken);

            if (existingRelationship is null)
            {
                return Err<FriendDto, FriendshipError>(FriendshipError.NoRequestFound);
            }

            if (existingRelationship.Status is FriendshipStatus.Accepted)
            {
                return Err<FriendDto, FriendshipError>(FriendshipError.AlreadyFriends);
            }

            if (existingRelationship.Status is FriendshipStatus.Rejected)
            {
                return Err<FriendDto, FriendshipError>(FriendshipError.AlreadyRejected);
            }

            if (existingRelationship.Status is FriendshipStatus.Pending)
            {
                existingRelationship.Status = FriendshipStatus.Accepted;
                existingRelationship.UpdateDate = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Notify the sender of the request of the acceptance
            FriendDto userWithStatus = await CreateFriendWithStatus(existingRelationship.ToUser!, existingRelationship.UpdateDate);

            await _socialHubContext.Clients
                .User(fromUserId.ToString())
                .OnFriendRequestAccepted(userWithStatus);

            // Return accepted friend with status
            FriendDto friendWithStatus = await CreateFriendWithStatus(existingRelationship.FromUser!, existingRelationship.UpdateDate);

            return Ok<FriendDto, FriendshipError>(friendWithStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while accepting friend requests from {fromUserId} to {toUserId}", fromUserId, toUserId);
            return Err<FriendDto, FriendshipError>(FriendshipError.UnknownError);
        }
    }


    public async Task<Result<Unit, FriendshipError>> RejectFriendRequest(
        Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        try
        {
            FriendshipDbo? existingRelationship = await _dbContext.UserFriendships
                .FirstOrDefaultAsync(r =>
                    r.FromUserId == fromUserId && r.ToUserId == toUserId, cancellationToken);

            if (existingRelationship is null)
            {
                return Err<Unit, FriendshipError>(FriendshipError.NoRequestFound);
            }

            if (existingRelationship.Status is FriendshipStatus.Accepted)
            {
                return Err<Unit, FriendshipError>(FriendshipError.AlreadyFriends);
            }

            if (existingRelationship.Status is FriendshipStatus.Rejected)
            {
                return Err<Unit, FriendshipError>(FriendshipError.AlreadyRejected);
            }

            if (existingRelationship.Status is FriendshipStatus.Pending)
            {
                existingRelationship.Status = FriendshipStatus.Rejected;
                existingRelationship.UpdateDate = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok<Unit, FriendshipError>(Unit());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while accepting friend requests from {fromUserId} to {toUserId}", fromUserId, toUserId);
            return Err<Unit, FriendshipError>(FriendshipError.UnknownError);
        }

    }

    public async Task<Result<Unit, FriendshipError>> RemoveFriendAsync(
        Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        try
        {
            int numDeleted = await GetFriendshipQuery(userId, friendId)
                .Where(r => r.Status == FriendshipStatus.Accepted)
                .ExecuteDeleteAsync(cancellationToken);

            if (numDeleted > 0)
            {
                // Notify the unfriended friend
                await _socialHubContext.Clients
                    .User(friendId.ToString())
                    .OnUnfriended(userId.ToString());

                return Ok<Unit, FriendshipError>(Unit());
            }

            return Err<Unit, FriendshipError>(FriendshipError.UserNotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting friend {friendId} of {userId}", friendId, userId);
            return Err<Unit, FriendshipError>(FriendshipError.UnknownError);
        }
    }

    public async Task<Result<IEnumerable<UserSearchResultDto>, FriendshipError>> SearchUsersAsync(string query)
    {
        try
        {
            // Normalize the query for case-insensitive search
            string normalizedQuery = query.Trim().ToLower();

            IQueryable<UserDbo> usersQuery = _dbContext.Users.AsQueryable();

            // Attempt to parse the query as a GUID
            if (Guid.TryParse(query, out Guid searchGuid))
            {
                // If the query is a valid GUID, search directly by id
                usersQuery = usersQuery.Where(u => u.Id == searchGuid);
            }
            else
            {
                // If not a GUID, search by username (case-insensitive)
                usersQuery = usersQuery.Where(u => 
                    u.Name.ToLower().Contains(normalizedQuery) ||
                    (u.LastPlayerName != null && u.LastPlayerName.ToLower().Contains(normalizedQuery)));
            }

            List<UserSearchResultDto> users = await usersQuery
                .Select(u => new UserSearchResultDto
                {
                    Id = u.Id.ToString(),
                    UserName = u.Name,
                    PlayerName = u.LastPlayerName,
                })
                .ToListAsync();

            return Ok<IEnumerable<UserSearchResultDto>, FriendshipError>(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while searching for users with search term {query}", query);
            return Err<IEnumerable<UserSearchResultDto>, FriendshipError>(FriendshipError.UnknownError);
        }
    }
}
