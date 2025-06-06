﻿using MatchmakingServer.Api;
using MatchmakingServer.Authentication.JWT;
using MatchmakingServer.Authentication.Passwordless;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer.Authentication;

public class PasswordlessAuthenticationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("auth");

        authGroup
            .MapGet("challenge", GetChallenge)
            .WithSummary("Initiates the authentication process by requesting a unique, one-time challenge.")
            .WithDescription(@"
                    Clients call this endpoint to obtain a random nonce (challenge string) and a corresponding ID. 
                    The client must then sign this challenge using its private key and send the signed result back to the login endpoint for verification.
                ");

        authGroup
            .MapPost("login", HandleAuthenticate)
            .WithSummary("Authenticates a client with the player name by verifying a signed challenge and issues a JWT.");
    }

    private static ChallengeResponse GetChallenge(PasswordlessAuthenticationService authService)
    {
        return authService.GenerateChallenge();
    }

    private static async Task<Results<Ok<TokenResponse>, BadRequest, NotFound, UnauthorizedHttpResult, StatusCodeHttpResult>> HandleAuthenticate(
        [FromBody] AuthenticationRequest request,
        [FromServices] PasswordlessAuthenticationService authService,
        CancellationToken cancellationToken)
    {
        AuthenticationResult result = await authService.AuthenticateAsync(request, cancellationToken);

        return result.Error switch
        {
            AuthenticationError.Success when result.TokenResponse is not null => TypedResults.Ok(result.TokenResponse),
            AuthenticationError.InvalidPublicKeyOrSignatureFormat => TypedResults.BadRequest(),
            AuthenticationError.ExpiredChallenge => TypedResults.NotFound(),
            AuthenticationError.VerificationFailed => TypedResults.Unauthorized(),
            _  => TypedResults.StatusCode(500)
        };
    }
}
