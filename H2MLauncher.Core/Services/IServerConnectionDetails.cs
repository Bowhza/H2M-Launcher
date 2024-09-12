namespace H2MLauncher.Core.Services
{
    public interface IServerConnectionDetails
    {
        string Ip { get; }

        int Port { get; }
    }

    public record struct ServerConnectionDetails(string Ip, int Port) : IServerConnectionDetails
    {
        public static implicit operator (string Ip, int Port)(ServerConnectionDetails value)
        {
            return (value.Ip, value.Port);
        }

        public static implicit operator ServerConnectionDetails((string Ip, int Port) value)
        {
            return new ServerConnectionDetails(value.Ip, value.Port);
        }
    }
}