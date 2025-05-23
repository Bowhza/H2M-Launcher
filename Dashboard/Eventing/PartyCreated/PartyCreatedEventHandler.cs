using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyCreated
{
    public sealed class PartyCreatedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyCreatedEvent>
    {
        public async Task Handle(PartyCreatedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyCreatedEvent(notification);
        }
    }
}
