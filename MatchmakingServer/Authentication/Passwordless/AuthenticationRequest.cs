namespace MatchmakingServer.Authentication.Passwordless;

public record AuthenticationRequest
{
    public required string ChallengeId { get; init; }

    public required string PublicKey { get; init; }

    public required string Signature { get; init; }

    public required string PlayerName { get; init; }
}
