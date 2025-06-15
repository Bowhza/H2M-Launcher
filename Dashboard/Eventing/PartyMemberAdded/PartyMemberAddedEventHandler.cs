using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyMemberAdded
{
    public sealed class PartyMemberAddedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyMemberAddedEvent>
    {
        public async Task Handle(PartyMemberAddedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyMemberAddedEvent(notification);
        }
    }
}
