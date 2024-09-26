using System.Net;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Utilities.SignalR;

/// <summary>
/// <see cref="IHubConnectionBuilder"/> implementation that builds a <see cref="CustomHubConnection"/>.
/// </summary>
internal class CustomHubConnectionBuilder : IHubConnectionBuilder
{
    private bool _hubConnectionBuilt;

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomHubConnectionBuilder"/> class.
    /// </summary>
    public CustomHubConnectionBuilder()
    {
        Services = new ServiceCollection();
        Services.AddSingleton<CustomHubConnection>();
        Services.AddLogging();
        this.AddJsonProtocol();
    }

    /// <inheritdoc />
    public CustomHubConnection Build()
    {
        // Build can only be used once
        if (_hubConnectionBuilt)
        {
            throw new InvalidOperationException("HubConnectionBuilder allows creation only of a single instance of HubConnection.");
        }

        _hubConnectionBuilt = true;

        // The service provider is disposed by the HubConnection
        var serviceProvider = Services.BuildServiceProvider();

        var connectionFactory = serviceProvider.GetService<IConnectionFactory>() ??
            throw new InvalidOperationException($"Cannot create {nameof(CustomHubConnection)} instance. An {nameof(IConnectionFactory)} was not configured.");

        var endPoint = serviceProvider.GetService<EndPoint>() ??
            throw new InvalidOperationException($"Cannot create {nameof(CustomHubConnection)} instance. An {nameof(EndPoint)} was not configured.");

        return serviceProvider.GetRequiredService<CustomHubConnection>();
    }

    HubConnection IHubConnectionBuilder.Build()
    {
        return Build();
    }
}
