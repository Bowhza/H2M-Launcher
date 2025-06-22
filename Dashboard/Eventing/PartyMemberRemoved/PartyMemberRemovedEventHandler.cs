using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyMemberRemoved
{
    public sealed class PartyMemberRemovedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyMemberRemovedEvent>
    {
        public async Task Handle(PartyMemberRemovedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyMemberRemovedEvent(notification);
        }
    }
}
