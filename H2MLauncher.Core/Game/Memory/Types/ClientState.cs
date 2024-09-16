using System.Runtime.InteropServices;

namespace H2MLauncher.Core.Services;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ClientState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x4A50)]
    public byte[] __pad0;

    public int Ping;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x8)]
    public byte[] pad1;

    public int NumPlayers;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] pad2;

    public int ServerTime;
}
