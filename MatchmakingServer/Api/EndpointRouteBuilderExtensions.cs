using MatchmakingServer.Authentication;
using MatchmakingServer.Stats;

namespace MatchmakingServer.Api;

public static class EndpointRouteBuilderExtensions
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapEndpoint<AuthenticationEndpoint>();
        app.MapEndpoint<PasswordlessAuthenticationEndpoint>();
        app.MapEndpoint<StatsEndpoint>();
    }

    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app) where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}
