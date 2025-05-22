namespace H2MLauncher.Core.OnlineServices.Authentication;

public record ChallengeResponse
{
    public required string ChallengeId { get; init; }

    public required string Nonce { get; init; }
}
