using System.Net;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.Game.Models
{
    public record GameState(bool VirtualLobbyLoaded, ConnectionState ConnectionState, IPEndPoint? Endpoint, DateTimeOffset? StartTime)
    {
        /**
         * == Joining game ==========================
         * CA_DISCONNECTED |      -      | VLL: true
         * CA_DISCONNECTED | <SERVER_IP> | VLL: false
         * CA_CONNECTING   | <SERVER_IP> | VLL: false
         * CA_SENDINGSTATS | <SERVER_IP> | VLL: false
         * CA_CONNECTED    | <SERVER_IP> | VLL: false
         * CA_LOADING      | <SERVER_IP> | VLL: false
         * CA_ACTIVE       | <SERVER_IP> | VLL: false
         * 
         * == Leaving game ==========================
         * CA_DISCONNECTED | <SERVER_IP> | VLL: false
         * CA_DISCONNECTED |      -      | VLL: true
         * CA_CHALLENGING  |      -      | VLL: true
         * CA_CONNECTED    |      -      | VLL: true
         * CA_LOADING      |      -      | VLL: true
         * CA_ACTIVE       |      -      | VLL: true
         * 
         * == Intermission ==========================
         * CA_DISCONNECTED |      -      | VLL: false
         * CA_DISCONNECTED | <SERVER_IP> | VLL: false
         * CA_CONNECTING   | <SERVER_IP> | VLL: false
         * CA_SENDINGSTATS | <SERVER_IP> | VLL: false
         * CA_CONNECTED    | <SERVER_IP> | VLL: false
         * CA_PRIMED       | <SERVER_IP> | VLL: false
         * CA_ACTIVE       | <SERVER_IP> | VLL: false
         */


        public bool IsConnected => ConnectionState is 
            ConnectionState.CA_CONNECTED or 
            >= ConnectionState.CA_LOADING && 
            !VirtualLobbyLoaded;

        public bool IsConnecting => ConnectionState is 
            ConnectionState.CA_CONNECTING or 
            ConnectionState.CA_CHALLENGING or 
            ConnectionState.CA_SENDINGSTATS;

        public bool IsInMainMenu => VirtualLobbyLoaded || ConnectionState < ConnectionState.CA_CONNECTING;

        public bool IsPrivateMatch => Endpoint?.Address.Equals(IPAddress.Any) == true && !IsInMainMenu;

        public TimeSpan TimeConnected => StartTime is not null ? DateTimeOffset.Now - StartTime.Value : TimeSpan.Zero;
    };
}
