using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace H2MLauncher.Core.Models
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

        public static bool TryParse(string address, [MaybeNullWhen(false)] out ServerConnectionDetails connectionDetails)
        {
            string[] splitted = address.Split(':');
            if (splitted.Length != 2)
            {
                connectionDetails = default;
                return false;
            }

            string ip = splitted[0];
            if (!int.TryParse(splitted[1], out int port))
            {
                connectionDetails = default;
                return false;
            }

            connectionDetails = (ip, port);
            return true;
        }
    }
}