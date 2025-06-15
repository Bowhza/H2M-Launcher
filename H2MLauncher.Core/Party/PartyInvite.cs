namespace H2MLauncher.Core.Party;

public record PartyInvite
{
    /// <summary>
    /// The party id, used to join.
    /// </summary>
    public required string PartyId { get; init; }

    /// <summary>
    /// The user id of the leader that created the invite.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Also supply the name of the sender, because they might not be friends.
    /// </summary>
    public required string SenderName { get; init; }

    /// <summary>
    /// Time when the invite expires.
    /// </summary>
    public required DateTime ExpirationTime { get; init; }
}
