using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.PartyMemberJoinedServer
{
    public sealed class PartyMemberJoinedServerEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<PartyMemberJoinedServerEvent>
    {
        public async Task Handle(PartyMemberJoinedServerEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceivePartyMemberJoinedServerEvent(notification);
        }
    }
}
