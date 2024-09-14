namespace MatchmakingServer
{
    class MMServerPriorityComparer : IComparer<GameServer>
    {
        public int Compare(GameServer? x, GameServer? y)
        {
            if (x?.LastServerInfo is null || y?.LastServerInfo is null)
            {
                return 0;
            }

            // Check if we should prioritize based on TotalScore < 1000
            bool xIsHalfFull = x.LastStatusResponse?.TotalScore < 1000 && x.LastServerInfo.RealPlayerCount < 6;
            bool yIsHalfFull = y.LastStatusResponse?.TotalScore < 1000 && x.LastServerInfo.RealPlayerCount < 6;

            // Case 1: If both servers are half empty and under the score limit,
            // prioritize by player count (servers with fewer players should be prioritized)
            if (xIsHalfFull && yIsHalfFull)
            {
                return x.LastServerInfo.RealPlayerCount.CompareTo(y.LastServerInfo.RealPlayerCount);
            }

            // Case 2: If one server is half full and under the score limit and the other one is not,
            // prioritize the half full server
            if (xIsHalfFull && !yIsHalfFull)
            {
                return -1;
            }
            if (!xIsHalfFull && yIsHalfFull)
            {
                return 1;
            }

            // Case 3: If both servers are over the score limit, prioritize by fewer players
            return x.LastServerInfo.RealPlayerCount.CompareTo(y.LastServerInfo.RealPlayerCount);
        }
    }
}
