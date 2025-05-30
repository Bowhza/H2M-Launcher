using MatchmakingServer.Authentication.JWT;

namespace MatchmakingServer.Authentication.Passwordless;

public record struct AuthenticationResult(TokenResponse? TokenResponse, AuthenticationError Error)
{
    public static implicit operator (TokenResponse? TokenResponse, AuthenticationError Error)(AuthenticationResult value)
    {
        return (value.TokenResponse, value.Error);
    }

    public static implicit operator AuthenticationResult((TokenResponse? TokenResponse, AuthenticationError Error) value)
    {
        return new AuthenticationResult(value.TokenResponse, value.Error);
    }
}