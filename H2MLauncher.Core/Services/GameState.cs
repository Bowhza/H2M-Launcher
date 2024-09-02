using System.Net;

namespace H2MLauncher.Core.Services
{
    public record GameState(bool VirtualLobbyLoaded, ConnectionState ConnectionState, IPEndPoint? Endpoint, DateTimeOffset? StartTime)
    {
        public bool IsConnected => ConnectionState >= ConnectionState.CA_CONNECTED && !IsInMainMenu;

        public bool IsConnecting => ConnectionState >= ConnectionState.CA_CONNECTING && !IsConnected;

        public bool IsInMainMenu => VirtualLobbyLoaded || ConnectionState < ConnectionState.CA_CONNECTING;

        public bool IsPrivateMatch => Endpoint?.Address.Equals(IPAddress.Any) == true;

        public TimeSpan TimeConnected => StartTime is not null ? DateTimeOffset.Now - StartTime.Value : TimeSpan.Zero;
    };
}
