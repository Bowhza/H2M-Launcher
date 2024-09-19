namespace H2MLauncher.Core.Services;

public enum ConnectionState
{
    CA_DISCONNECTED = 0x0,
    CA_CINEMATIC = 0x1,
    CA_LOGO = 0x2,
    CA_CONNECTING = 0x3,
    CA_CHALLENGING = 0x4,
    CA_CONNECTED = 0x5,
    CA_SENDINGSTATS = 0x6,
    CA_SYNCHRONIZING_DATA = 0x7,
    CA_LOADING = 0x8,
    CA_PRIMED = 0x9,
    CA_ACTIVE = 0xA,
}
