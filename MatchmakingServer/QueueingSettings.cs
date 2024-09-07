namespace MatchmakingServer;

public record QueueingSettings
{
    /// <summary>
    /// Inactivity timeout until the server processing stops.
    /// </summary>
    public int QueueInactivityIdleTimeoutInS { get; init; } = 3 * 60;

    /// <summary>
    /// The maximum amount of time a player can block the queue since the first time a slot becomes available for him.
    /// </summary>
    public int TotalJoinTimeLimitInS { get; init; } = 50;

    /// <summary>
    /// The timeout for a join request after which the player will be removed from the queue.
    /// </summary>
    public int JoinTimeoutInS { get; init; } = 30;

    /// <summary>
    /// Whether to reset join attempts and therefore the total timeout 
    /// when the server was full at the time of a negative join ack.
    /// </summary>
    public bool ResetJoinAttemptsWhenServerFull { get; init; } = true;

    /// <summary>
    /// The maximum number of join attempts for a player per queue.
    /// </summary>
    public int MaxJoinAttempts { get; init; } = 3;

    /// <summary>
    /// The maximum number of players allowed per server queue.
    /// </summary>
    public int QueuePlayerLimit { get; init; } = 50;

    /// <summary>
    /// Whether to use the Webfront API to check for players names of joining players.
    /// </summary>
    public bool ConfirmJoinsWithWebfrontApi { get; init; } = false;

    /// <summary>
    /// Whether to remove a server entirely when the queue stopped.
    /// </summary>
    public bool CleanupServerWhenStopped { get; init; } = false;
}
