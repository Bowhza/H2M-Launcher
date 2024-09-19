using System.Runtime.InteropServices;

namespace H2MLauncher.Core.Services;

// Struct matching connect_state_t
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ConnectState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] // To match the __pad0[0xC]
    public byte[] __pad0;
    public NetAddress Address;
}
