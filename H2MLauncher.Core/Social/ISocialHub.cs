namespace MatchmakingServer.Core.Social;

public interface ISocialHub
{
    Task UpdatePlayerName(string newPlayerName);

    Task UpdateGameStatus(GameStatus newGameStatus);
}
