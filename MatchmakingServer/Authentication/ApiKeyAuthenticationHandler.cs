using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace MatchmakingServer.Authentication
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(ApiKeyDefaults.RequestHeaderKey, out var apiKeyValues))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));
            }

            string? providedApiKey = apiKeyValues.FirstOrDefault();
            string? expectedApiKey = Options.ApiKey;

            if (string.IsNullOrEmpty(providedApiKey) || providedApiKey != expectedApiKey)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
            }

            Claim[] claims = [new Claim(ClaimTypes.Name, "APIKeyUser")];

            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
