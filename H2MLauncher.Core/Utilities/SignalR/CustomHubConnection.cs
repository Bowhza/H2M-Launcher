using System.Net;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Utilities.SignalR;

/// <summary>
/// Custom hub connection that has a <see cref="Connected"/> event.
/// </summary>
internal class CustomHubConnection : HubConnection
{
    public CustomHubConnection(IConnectionFactory connectionFactory, IHubProtocol protocol, EndPoint endPoint, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        : base(connectionFactory, protocol, endPoint, serviceProvider, loggerFactory)
    {
    }

    public CustomHubConnection(IConnectionFactory connectionFactory, IHubProtocol protocol, EndPoint endPoint, IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IRetryPolicy reconnectPolicy)
        : base(connectionFactory, protocol, endPoint, serviceProvider, loggerFactory, reconnectPolicy)
    {
    }

    public event Func<string?, Task>? Connected;

    private void RunConnectedEvent(string? clientId)
    {
        var connected = Connected;

        async Task RunConnectedEventAsync()
        {
            await Task.Yield();

            try
            {
                await connected.Invoke(clientId).ConfigureAwait(false);
            }
            catch
            {

            }
        }

        // There is no need to start a new task if there is no Connected event registered
        if (connected != null)
        {
            // Fire-and-forget the connected event
            _ = RunConnectedEventAsync();
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);

        RunConnectedEvent(ConnectionId);
    }
}
