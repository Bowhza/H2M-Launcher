namespace Dashboard.Party;

public record PartySnapshotAddedNotification(PartySnapshot NewSnapshot) : IEvent;
