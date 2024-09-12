namespace H2MLauncher.Core.Models
{
    public record struct ServerPing(string Ip, int Port, uint Ping)
    {
        public static implicit operator (string Ip, int Port, uint Ping)(ServerPing value)
        {
            return (value.Ip, value.Port, value.Ping);
        }

        public static implicit operator ServerPing((string Ip, int Port, uint Ping) value)
        {
            return new ServerPing(value.Ip, value.Port, value.Ping);
        }
    }
}
