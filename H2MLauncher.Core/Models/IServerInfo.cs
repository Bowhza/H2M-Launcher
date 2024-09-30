namespace H2MLauncher.Core.Models;

public interface IServerInfo : ISimpleServerInfo
{
    public int MaxClients { get; }

    public int Clients { get; }

    public int Bots { get; }

    public int RealPlayerCount { get; }

    public int PrivilegedSlots { get; }

    public bool HasMap { get; }

    public bool IsPrivate { get; }
}
