using System.Security.Claims;

namespace MatchmakingServer.Authentication.Passwordless;

internal record struct AuthenticationResult(ClaimsIdentity? Identity, AuthenticationError Error)
{
    public static implicit operator (ClaimsIdentity? Identity, AuthenticationError Error)(AuthenticationResult value)
    {
        return (value.Identity, value.Error);
    }

    public static implicit operator AuthenticationResult((ClaimsIdentity? Identity, AuthenticationError Error) value)
    {
        return new AuthenticationResult(value.Identity, value.Error);
    }
}