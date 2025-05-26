using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace MatchmakingServer.Authorization;

public record IsOwnerRequirement(string UserIdClaimType) : IAuthorizationRequirement;

public class IsOwnerHandler : AuthorizationHandler<IsOwnerRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IsOwnerHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IsOwnerRequirement requirement)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        Claim? authenticatedUserIdClaim = context.User.FindFirst(requirement.UserIdClaimType);
        if (authenticatedUserIdClaim == null)
        {
            context.Fail(); // User is not authenticated or lacks the ID claim
            return Task.CompletedTask;
        }

        Guid authenticatedUserId = Guid.Parse(authenticatedUserIdClaim.Value);

        // Get the requested userId from the route
        RouteData? routeData = httpContext.GetRouteData();
        if (!routeData.Values.TryGetGuidValue("userId", out Guid? requestedUserId))
        {
            // Route parameter missing or invalid
            context.Fail();
            return Task.CompletedTask;
        }

        // 1. Check if it's the user's own friends list
        if (authenticatedUserId == requestedUserId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        context.Fail();
        return Task.CompletedTask;
    }
}
