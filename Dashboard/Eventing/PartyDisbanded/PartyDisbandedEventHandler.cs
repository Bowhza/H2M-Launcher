using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyDisbanded
{
    public sealed class PartyDisbandedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyDisbandedEvent>
    {
        public async Task Handle(PartyDisbandedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyDisbandedEvent(notification);
        }
    }
}
