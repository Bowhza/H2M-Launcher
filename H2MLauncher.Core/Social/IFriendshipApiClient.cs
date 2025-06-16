using H2MLauncher.Core.Social.Friends;

using Refit;

namespace H2MLauncher.Core.Social;

[Headers("Authorization: Bearer")]
public interface IFriendshipApiClient
{
    [Get("/users/{userId}/friends")]
    Task<IApiResponse<List<FriendDto>>> GetFriendsAsync(string userId, CancellationToken cancellationToken = default);

    [Get("/users/{userId}/friends/{friendId}")]
    Task<IApiResponse<FriendDto>> GetFriendAsync(string userId, string friendId, CancellationToken cancellationToken = default);


    [Get("/users/{userId}/friend-requests")]
    Task<IApiResponse<List<FriendRequestDto>>> GetFriendRequestsAsync(string userId, CancellationToken cancellationToken = default);

    [Get("/users/{userId}/friend-requests/{friendId}")]
    Task<IApiResponse<FriendRequestDto>> GetFriendRequestAsync(string userId, string friendId, CancellationToken cancellationToken = default);


    [Post("/users/{userId}/friend-requests")]
    Task<IApiResponse<FriendRequestDto>> SendFriendRequestAsync(string userId, [Body] SendFriendRequestDto target, CancellationToken cancellationToken = default);


    [Put("/users/{userId}/friends/{friendId}")]
    Task<IApiResponse<FriendDto>> AcceptFriendRequestAsync(string userId, string friendId, CancellationToken cancellationToken = default);

    [Delete("/users/{userId}/friend-requests/{friendId}")]
    Task<IApiResponse> RejectFriendRequestAsync(string userId, string friendId, CancellationToken cancellationToken = default);


    [Delete("/users/{userId}/friends/{friendId}")]
    Task<IApiResponse> UnfriendAsync(string userId, string friendId, CancellationToken cancellationToken = default);


    [Get("/friends/search")]
    Task<IApiResponse<List<UserSearchResultDto>>> SearchFriendsAsync(string query, CancellationToken cancellationToken = default);
}
