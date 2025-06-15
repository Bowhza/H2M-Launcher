using Dashboard.Eventing.PartyMemberLeft;
using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyMemberLeftServer
{
    public sealed class PartyMemberLeftServerEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyMemberLeftServerEvent>
    {
        public async Task Handle(PartyMemberLeftServerEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyMemberLeftServerEvent(notification);
        }
    }
}
