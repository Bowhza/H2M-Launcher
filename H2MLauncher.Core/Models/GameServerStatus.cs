using System.Net;

namespace H2MLauncher.Core.Models
{
    public record GameServerStatus
    {
        public required IPEndPoint Address { get; init; }

        public List<GamePlayerStatus> Players { get; init; } = [];

        /// <summary>
        /// Calculates the total score of all players.
        /// </summary>
        public int TotalScore => Players.Sum(p => p.Score);
    }

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
