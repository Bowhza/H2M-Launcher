﻿namespace H2MLauncher.Core.Social.Friends;

public record UserSearchResultDto
{
    public required string Id { get; init; }

    public required string UserName { get; init; }

    public string? PlayerName { get; init; }
}
