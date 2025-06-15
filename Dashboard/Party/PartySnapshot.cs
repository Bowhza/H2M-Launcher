namespace Dashboard.Party;

public class PartySnapshot
{
    public long Id { get; set; } // Primary Key

    public required string PartyId { get; set; }

    public int Size { get; set; }

    public DateTimeOffset Timestamp { get; set; } // When this snapshot was taken
}
