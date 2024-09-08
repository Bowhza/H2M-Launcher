using Microsoft.AspNetCore.Authentication;

namespace MatchmakingServer.Authentication
{
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string? ApiKey { get; set; }
    }
}
