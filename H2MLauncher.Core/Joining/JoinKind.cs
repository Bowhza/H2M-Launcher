namespace H2MLauncher.Core.Joining;

/// <summary>
/// Kind of a join operation.
/// </summary>
public enum JoinKind
{
    /// <summary>
    /// User triggered the join manually.
    /// </summary>
    Normal,

    /// <summary>
    /// Automatically joined from the server queue.
    /// </summary>
    FromQueue,

    /// <summary>
    /// Join from the party.
    /// </summary>
    FromParty,

    /// <summary>
    /// Forced join.
    /// </summary>
    Forced,

    /// <summary>
    /// Rejoined the last server.
    /// </summary>
    Rejoin,

    /// <summary>
    /// Other backend notification triggered join.
    /// </summary>
    Backend
}
