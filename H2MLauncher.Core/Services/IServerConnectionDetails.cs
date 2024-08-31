namespace H2MLauncher.Core.Services
{
    public interface IServerConnectionDetails
    {
        string Ip { get; }

        int Port { get; }
    }
}