using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyQueueCancelled
{
    public sealed class PartyQueueCancelledEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyQueueCancelledEvent>
    {
        public async Task Handle(PartyQueueCancelledEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyQueueCancelledEvent(notification);
        }
    }
}
