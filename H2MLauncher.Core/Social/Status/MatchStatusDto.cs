using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Social.Status;

public record MatchStatusDto(
    ServerConnectionDetails Server, 
    string ServerName, 
    string? GameMode, 
    string? MapName, 
    DateTimeOffset JoinedAt);