using CommunityToolkit.Mvvm.Messaging;

using Microsoft.AspNetCore.SignalR.Client;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Utilities.SignalR;

/// <summary>
/// Base implementation of a SignalR client based on a <see cref="HubConnection"/>.
/// Provides methods for connection events and uses <see cref="TypedSignalR"/> for proxy.
/// </summary>
/// <typeparam name="THub">The hub contract interface type (same as implemented by server hub).</typeparam>
public abstract class HubClient<THub> : IDisposable
    where THub : class
{
    private bool _disposedValue;
    private readonly IDisposable _hubConnectionObserverReg;
    private readonly HubConnectionObserver _hubConnectionObserver;

    /// <summary>
    /// Gets the underlying connection to the hub.
    /// </summary>
    protected HubConnection Connection { get; }

    /// <summary>
    /// Gets the hub proxy to invoke server methods.
    /// </summary>
    protected THub Hub { get; }

    /// <summary>
    /// Gets a <see cref="CancellationTokenSource"/> to cancel all hub proxy requests.
    /// </summary>
    protected CancellationTokenSource HubCancellation { get; } = new();

    /// <summary>
    /// Whether the client is connected to the server.
    /// </summary>
    public bool IsConnected => Connection.State is HubConnectionState.Connected;

    /// <summary>
    /// Whether the client is (re)connecting to the server.
    /// </summary>
    public bool IsConnecting => Connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting;

    /// <summary>
    /// Raised when the connection is opened or closed;
    /// </summary>
    public event Action<bool>? ConnectionChanged;

    public HubClient(HubConnection hubConnection)
    {
        Connection = hubConnection;
        Hub = CreateHubProxy(hubConnection, HubCancellation.Token);

        _hubConnectionObserver = new(this);
        _hubConnectionObserverReg = Connection.Register<IHubConnectionObserver>(_hubConnectionObserver);
    }

    /// <summary>
    /// Overriden to create a hub proxy object for the <paramref name="hubConnection"/> with a
    /// <paramref name="hubCancellationToken"/> used to cancel the invocations.
    /// </summary>
    protected abstract THub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken);

    /// <summary>
    /// Starts the connection if disconnected.
    /// </summary>
    /// <returns>Whether the connection is established.</returns>
    public async Task<bool> StartConnection(CancellationToken cancellationToken = default)
    {
        if (Connection.State is HubConnectionState.Disconnected)
        {
            await Connection.StartAsync(cancellationToken);
            await OnConnected(cancellationToken);
        }

        return Connection.State is HubConnectionState.Connected;
    }

    /// <summary>
    /// Stops the connection if not already disconnected.
    /// </summary>
    public Task StopConnection(CancellationToken cancellationToken)
    {
        if (Connection.State is HubConnectionState.Disconnected)
        {
            return Task.CompletedTask;
        }

        return Connection.StopAsync(cancellationToken);
    }

    protected virtual Task OnConnected(CancellationToken cancellationToken = default)
    {
        ConnectionChanged?.Invoke(true);
        return Task.CompletedTask;
    }

    protected virtual Task OnConnectionClosed(Exception? exception)
    {
        ConnectionChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    protected virtual Task OnReconnecting(Exception? exception)
    {
        ConnectionChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    protected virtual Task OnReconnected(string? connectionId)
    {
        ConnectionChanged?.Invoke(true);
        return Task.CompletedTask;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _hubConnectionObserverReg.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private sealed class HubConnectionObserver(HubClient<THub> client) : IHubConnectionObserver
    {
        private readonly HubClient<THub> _client = client;

        public Task OnClosed(Exception? exception)
        {
            return _client.OnConnectionClosed(exception);
        }

        public Task OnReconnected(string? connectionId)
        {
            return _client.OnReconnected(connectionId);
        }

        public Task OnReconnecting(Exception? exception)
        {
            return _client.OnReconnecting(exception);
        }
    }
}
