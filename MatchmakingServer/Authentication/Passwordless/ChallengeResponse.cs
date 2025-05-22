namespace MatchmakingServer.Authentication.Passwordless;

public record ChallengeResponse
{
    public required string ChallengeId { get; init; }

    public required string Nonce { get; init; }
}
