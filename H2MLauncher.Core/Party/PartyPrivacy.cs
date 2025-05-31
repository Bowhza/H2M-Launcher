namespace H2MLauncher.Core.Party;

public enum PartyPrivacy
{
    /// <summary>
    /// The party is closed - joining is only possible via invites.
    /// </summary>
    Closed,

    /// <summary>
    /// The party is only open to friends for joining.
    /// </summary>
    Friends,

    /// <summary>
    /// Everyone can join the party.
    /// </summary>
    Open
}
