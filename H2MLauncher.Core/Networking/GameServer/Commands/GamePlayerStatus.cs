namespace H2MLauncher.Core.Networking.GameServer
{
    public readonly record struct GamePlayerStatus(int Score, int Ping, string PlayerName)
    {
        public static implicit operator (int Score, int Ping, string PlayerName)(GamePlayerStatus value)
        {
            return (value.Score, value.Ping, value.PlayerName);
        }

        public static implicit operator GamePlayerStatus((int Score, int Ping, string PlayerName) value)
        {
            return new GamePlayerStatus(value.Score, value.Ping, value.PlayerName);
        }
    }
}
