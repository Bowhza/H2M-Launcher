namespace MatchmakingServer.Authentication.JWT;

public class TokenResponse
{
    public required string AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public string TokenType { get; set; } = "";

    public required int ExpiresIn { get; set; }
}
