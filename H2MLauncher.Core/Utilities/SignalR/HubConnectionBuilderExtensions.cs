using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Utilities.SignalR;

internal static class HubConnectionBuilderExtensions
{
    /// <summary>
    /// Configures the <see cref="HttpConnectionOptions"/> to negotiate stateful reconnect with the server.
    /// </summary>
    /// <param name="hubConnectionBuilder">The <see cref="IHubConnectionBuilder" /> to configure.</param>
    /// <returns>The same instance of the <see cref="IHubConnectionBuilder"/> for chaining.</returns>
    public static IHubConnectionBuilder WithStatefulReconnect(this IHubConnectionBuilder hubConnectionBuilder, long bufferSize)
    {
        hubConnectionBuilder.Services.Configure<HttpConnectionOptions>(options => options.UseStatefulReconnect = true);
        hubConnectionBuilder.Services.Configure<HubConnectionOptions>(options => options.StatefulReconnectBufferSize = bufferSize);

        return hubConnectionBuilder;
    }
}
