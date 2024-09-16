using System.Runtime.InteropServices;

namespace H2MLauncher.Core.Services;

// Struct matching netadr_s
[StructLayout(LayoutKind.Sequential)]
public struct NetAddress
{
    public NetAddressType Type;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] IP;

    public ushort Port;

    public NetSrc localNetID;

    public uint AddrHandleIndex;
}
