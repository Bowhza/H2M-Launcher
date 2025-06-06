using Dashboard.Hubs;

using MediatR;

using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Eventing.OnlineUserCountChanged
{
    public sealed class OnlineUserCountChangedEventHandler(IHubContext<InteractiveLauncherHub, IInteractiveLauncher> hubContext) : INotificationHandler<OnlineUserCountChangedEvent>
    {
        public async Task Handle(OnlineUserCountChangedEvent notification, CancellationToken cancellationToken)
        {
            await hubContext.Clients.All.ReceiveOnlineUserCountChangedEvent(notification);
        }
    }
}
