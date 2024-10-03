namespace MatchmakingServer.Matchmaking.Models
{
    public readonly record struct EligibilityResult(bool IsEligibile, string? Reason);
}
