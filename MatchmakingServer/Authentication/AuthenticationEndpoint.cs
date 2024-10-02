using System.Security.Claims;

using FluentValidation;

using MatchmakingServer.Api;

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MatchmakingServer.Authentication
{
    public class AuthenticationEndpoint : IEndpoint
    {
        public static void Map(IEndpointRouteBuilder app) => app
            .MapGet("/login", Handle)
            .WithSummary("Logs the user in with the UID and player name and returns a bearer token.")
            .WithValidation<Request>();

        public record Request(string Uid, string PlayerName);
        public class RequestValidator : AbstractValidator<Request>
        {
            public RequestValidator()
            {
                RuleFor(x => x.Uid).NotEmpty().MaximumLength(36);
                RuleFor(x => x.PlayerName).NotEmpty().MaximumLength(69);
            }
        }

        private static SignInHttpResult Handle([AsParameters] Request request)
        {
            ClaimsPrincipal claimsPrincipal = new(
                 new ClaimsIdentity(
                   [new Claim(ClaimTypes.Name, request.PlayerName), new Claim(ClaimTypes.NameIdentifier, request.Uid)],
                   BearerTokenDefaults.AuthenticationScheme
                 )
               );

            return TypedResults.SignIn(claimsPrincipal);
        }
    }
}
