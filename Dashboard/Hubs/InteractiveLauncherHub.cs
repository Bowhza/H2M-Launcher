using Dashboard.Eventing.OnlineUserCountChanged;
using Dashboard.Eventing.PartyCreated;
using Dashboard.Eventing.PartyDisbanded;
using Dashboard.Eventing.PartyMemberAdded;
using Dashboard.Eventing.PartyMemberJoinedServer;
using Dashboard.Eventing.PartyMemberLeft;
using Dashboard.Eventing.PartyMemberRemoved;
using Dashboard.Eventing.PartyQueueCancelled;
using Dashboard.Eventing.PartyQueued;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Hubs
{
    public interface IInteractiveLauncher
    {
        // General party events (update public party interface)
        Task ReceivePartyCreatedEvent(PartyCreatedEvent partyCreatedEvent);
        Task ReceivePartyMemberAddedEvent(PartyMemberAddedEvent partyMemberAddedEvent);
        Task ReceivePartyMemberRemovedEvent(PartyMemberRemovedEvent partyMemberRemovedEvent);
        Task ReceivePartyDisbandedEvent(PartyDisbandedEvent partyDisbandedEvent);

        // Queueing
        Task ReceivePartyQueuedEvent(PartyQueuedEvent partyQueuedEvent);
        Task ReceivePartyQueueCancelledEvent(PartyQueueCancelledEvent partyQueueCancelledEvent);
        Task ReceivePartyMemberJoinedServerEvent(PartyMemberJoinedServerEvent partyMemberJoinedServerEvent);
        // could indicate in the UI that one person is still actively in the party, but decided to not play this match (left game, but still in party)
        Task ReceivePartyMemberLeftServerEvent(PartyMemberLeftServerEvent partyMemberLeftServerEvent);

        // Online users of the launcher
        Task ReceiveOnlineUserCountChangedEvent(OnlineUserCountChangedEvent onlineUserCountChangedEvent);
    }

    public class InteractiveLauncherHub : Hub<IInteractiveLauncher>
    {
        public InteractiveLauncherHub() { }
    }
}
