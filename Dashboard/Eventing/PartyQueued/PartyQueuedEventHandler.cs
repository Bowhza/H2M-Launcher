using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyQueued
{
    public sealed class PartyQueuedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyQueuedEvent>
    {
        public async Task Handle(PartyQueuedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyQueuedEvent(notification);
        }
    }
}
