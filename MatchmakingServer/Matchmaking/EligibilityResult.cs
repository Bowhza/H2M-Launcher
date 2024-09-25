namespace MatchmakingServer
{
    public readonly record struct EligibilityResult(bool IsEligibile, string? Reason);
}
