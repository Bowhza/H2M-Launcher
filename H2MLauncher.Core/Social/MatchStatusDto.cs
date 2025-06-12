using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Social;

public record MatchStatusDto(ServerConnectionDetails Server, string? GameMode, string? MapName, DateTimeOffset JoinedAt);