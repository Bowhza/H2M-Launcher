namespace H2MLauncher.Core.Social.Friends;

public record FriendRequestDto
{
    public required Guid UserId { get; init; }

    public required string UserName { get; init; }

    public string? PlayerName { get; init; }

    public required FriendRequestStatus Status { get; init; }

    public DateTimeOffset Created { get; init; }
}
