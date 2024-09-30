using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using H2MLauncher.Core.Utilities;

namespace H2MLauncher.Core.Models
{
    [JsonConverter(typeof(ServerConnectionDetailsJsonConverter))]
    [TypeConverter(typeof(ServerConnectionDetailsTypeConverter))]
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

        public readonly override string ToString()
        {
            return $"{Ip}:{Port}";
        }
    }
}