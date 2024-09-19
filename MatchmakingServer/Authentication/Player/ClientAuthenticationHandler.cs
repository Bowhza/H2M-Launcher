using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MatchmakingServer.Authentication.Player
{
    public class ClientAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public ClientAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var endpoint = Context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // Extract token or user info from request query parameters
            string? uniqueId = Request.Query["uid"].SingleOrDefault();
            string? playerName = Request.Query["playerName"].SingleOrDefault();

            if (!string.IsNullOrEmpty(uniqueId) && !string.IsNullOrEmpty(playerName))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, uniqueId),
                    new Claim(ClaimTypes.Name, playerName)
                };

                var identity = new ClaimsIdentity(claims, "client");
                var principal = new ClaimsPrincipal(identity);

                var ticket = new AuthenticationTicket(principal, "client");

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.Fail("User id and player name required"));
        }
    }
}
