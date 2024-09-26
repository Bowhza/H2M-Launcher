using CommunityToolkit.Mvvm.Messaging;

using Microsoft.AspNetCore.SignalR.Client;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Utilities.SignalR;

public abstract class HubClient<THub> : IDisposable
    where THub : class
{
    private bool _disposedValue;
    private readonly IDisposable _hubConnectionObserverReg;
    private readonly HubConnectionObserver _hubConnectionObserver;

    protected HubConnection Connection { get; }
    protected THub Hub { get; }
    protected CancellationTokenSource HubCancellation { get; } = new();

    public bool IsConnected => Connection.State is HubConnectionState.Connected;
    public bool IsConnecting => Connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting;

    public event Action<bool>? ConnectionChanged;

    public HubClient(HubConnection hubConnection)
    {
        Connection = hubConnection;
        Hub = CreateHubProxy(hubConnection, HubCancellation.Token);

        _hubConnectionObserver = new(this);
        _hubConnectionObserverReg = Connection.Register<IHubConnectionObserver>(_hubConnectionObserver);
    }

    protected abstract THub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken);

    public async Task StartConnection(CancellationToken cancellationToken = default)
    {
        if (Connection.State is HubConnectionState.Disconnected)
        {
            await Connection.StartAsync(cancellationToken);
            await OnConnected(cancellationToken);
        }
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
        return Task.CompletedTask;
    }

    protected virtual Task OnReconnected(string? connectionId)
    {
        return Task.CompletedTask;
    }

    public Task StopConnection(CancellationToken cancellationToken)
    {
        if (Connection.State is HubConnectionState.Disconnected)
        {
            return Task.CompletedTask;
        }

        return Connection.StopAsync(cancellationToken);
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
