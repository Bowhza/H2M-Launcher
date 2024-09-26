namespace H2MLauncher.Core.Models
{
    public interface IServerConnectionDetails
    {
        string Ip { get; }

        int Port { get; }

        public string? GetAddress() => $"{Ip}:{Port}";
    }
}