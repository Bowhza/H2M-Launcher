using System.ComponentModel.DataAnnotations;

using FxKit;

using H2MLauncher.Core.Social.Friends;

using MatchmakingServer.Authorization;
using MatchmakingServer.Social;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer.Controllers;

[Authorize]
[Route("api")]
[Route("")] // NOTE: for compatibility
public class FriendsController : ControllerBase
{
    private readonly FriendshipsService _friendshipsService;

    public FriendsController(FriendshipsService friendshipsService)
    {
        _friendshipsService = friendshipsService;
    }

    [HttpGet("users/{userId}/friends")]
    [Authorize(Policy = Policies.CanReadFriends)]
    public async Task<IActionResult> GetAllFriends(Guid userId, CancellationToken cancellationToken)
    {
        Result<List<FriendDto>, FriendshipError> result =
            await _friendshipsService.GetFriendsWithStatusAsync(userId, cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            (error) => error switch
            {
                FriendshipError.UserNotFound => NotFound(new { error = "User not found" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpGet("users/{userId}/friends/{friendId}")]
    [Authorize(Policy = Policies.CanReadFriends)]
    public async Task<IActionResult> GetFriend(Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        Result<FriendDto, FriendshipError> result =
            await _friendshipsService.GetFriendWithStatusAsync(userId, friendId, cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            (error) => error switch
            {
                FriendshipError.UserNotFound => NotFound(new { error = "User or friend not found" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpDelete("users/{userId}/friends/{friendId}")]
    [Authorize(Policy = Policies.CanRemoveFriend)]
    public async Task<IActionResult> Unfriend(Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        Result<Unit, FriendshipError> result =
            await _friendshipsService.RemoveFriendAsync(userId, friendId, cancellationToken);

        return result.Match<IActionResult>(
            (_) => NoContent(),
            (error) => error switch
            {
                FriendshipError.UserNotFound => NotFound(new { error = "User or friend not found" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpGet("users/{userId}/friend-requests/{friendId}")]
    [Authorize(Policy = Policies.AccessFriendRequests)]
    public async Task<IActionResult> GetFriendRequest(Guid userId, Guid friendId, CancellationToken cancellationToken)
    {
        Option<FriendRequestDto> result = await _friendshipsService.GetFriendRequest(userId, friendId, cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            NotFound
        );
    }

    [HttpGet("users/{userId}/friend-requests")]
    [Authorize(Policy = Policies.AccessFriendRequests)]
    public async Task<IActionResult> GetFriendRequests(Guid userId, CancellationToken cancellationToken)
    {
        Result<List<FriendRequestDto>, FriendshipError> result =
            await _friendshipsService.GetFriendRequests(userId, cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            (error) => error switch
            {
                FriendshipError.UserNotFound => NotFound(new { error = "User not found" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpPost("users/{userId}/friend-requests")]
    [Authorize(Policy = Policies.AccessFriendRequests)]
    public async Task<IActionResult> CreateFriendRequest(Guid userId, [FromBody] SendFriendRequestDto request, CancellationToken cancellationToken)
    {
        Result<FriendRequestDto, FriendshipError> result =
            await _friendshipsService.SendFriendRequest(userId, request.TargetUserId, cancellationToken);

        return result.Match<IActionResult>(
            (friendRequest) => CreatedAtAction(nameof(GetFriendRequest), new { userId, friendId = request.TargetUserId }, friendRequest),
            (error) => error switch
            {
                FriendshipError.UserNotFound => NotFound(new { error = "User not found" }),
                FriendshipError.RequestToYourself => BadRequest(new { error = "Cannot send friend request to yourself" }),
                FriendshipError.AlreadyFriends => Conflict(new { error = "Already friends" }),
                FriendshipError.AlreadyRejected or
                FriendshipError.RequestPending => Conflict(new { error = "Friend request already exists" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpPut("users/{userId}/friends/{senderId}")]
    [Authorize(Policy = Policies.AccessFriendRequests)]
    public async Task<IActionResult> AcceptFriendRequest(Guid userId, Guid senderId, CancellationToken cancellationToken)
    {
        Result<FriendDto, FriendshipError> result = await _friendshipsService
            .AcceptFriendRequest(fromUserId: senderId, toUserId: userId, cancellationToken);

        return result.Match<IActionResult>(
            (friend) => CreatedAtAction(nameof(GetFriend), new { userId, friendId = senderId }, friend),
            (error) => error switch
            {
                FriendshipError.NoRequestFound or FriendshipError.AlreadyFriends => NotFound(new { error = "No friend request" }),
                FriendshipError.AlreadyRejected => Conflict(new { error = "Already rejected" }),
                _ => StatusCode(500)
            }
        );
    }

    [HttpDelete("users/{userId}/friend-requests/{senderId}")]
    [Authorize(Policy = Policies.AccessFriendRequests)]
    public async Task<IActionResult> RejectFriendRequest(Guid userId, Guid senderId, CancellationToken cancellationToken)
    {
        Result<Unit, FriendshipError> result = await _friendshipsService
            .RejectFriendRequest(fromUserId: senderId, toUserId: userId, cancellationToken);

        return result.Match<IActionResult>(
            (_) => NoContent(),
            (error) => error switch
            {
                FriendshipError.NoRequestFound or FriendshipError.AlreadyFriends => NotFound(new { error = "Friend request not found" }),
                FriendshipError.AlreadyRejected => Conflict(new { error = "Already rejected" }),
                _ => StatusCode(500)
            }
        );
    }

    /// <summary>
    /// Searches for users by ID (GUID) or user name.
    /// </summary>
    /// <param name="query">The search term (GUID string or user name).</param>
    /// <returns>A list of matching users.</returns>
    [HttpGet("friends/search")]
    public async Task<ActionResult<IEnumerable<UserSearchResultDto>>> SearchUsers(
        [FromQuery]
        [Required]
        [MinLength(3)]
        string query)
    {
        Result<IEnumerable<UserSearchResultDto>, FriendshipError> result = await _friendshipsService.SearchUsersAsync(query);

        return result.Match<ActionResult<IEnumerable<UserSearchResultDto>>>(
            (results) => Ok(results),
            (error) => StatusCode(500)
        );
    }
}
