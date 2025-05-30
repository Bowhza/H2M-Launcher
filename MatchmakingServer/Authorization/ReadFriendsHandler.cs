using System.Security.Claims;

using MatchmakingServer.Social;

using Microsoft.AspNetCore.Authorization;

namespace MatchmakingServer.Authorization;

public record ReadFriendsRequirement(string UserIdClaimType) : IAuthorizationRequirement;

public class ReadFriendsHandler : AuthorizationHandler<ReadFriendsRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FriendshipsService _socialService;

    public ReadFriendsHandler(IHttpContextAccessor httpContextAccessor, FriendshipsService socialService)
    {
        _httpContextAccessor = httpContextAccessor;
        _socialService = socialService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ReadFriendsRequirement requirement)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            context.Fail();
            return;
        }

        Claim? authenticatedUserIdClaim = context.User.FindFirst(requirement.UserIdClaimType);
        if (authenticatedUserIdClaim is null)
        {
            context.Fail(); // User is not authenticated or lacks the ID claim
            return;
        }

        Guid authenticatedUserId = Guid.Parse(authenticatedUserIdClaim.Value);

        // Get the requested userId from the route
        RouteData? routeData = httpContext.GetRouteData();
        if (!routeData.Values.TryGetGuidValue("userId", out Guid? requestedUserId))
        {
            // Route parameter missing or invalid
            context.Fail();
            return;
        }

        // Get the optional requested friendId
        routeData.Values.TryGetGuidValue("friendId", out Guid? requestedFriendId);

        // 1. Check if it's the user's own friends list
        if (authenticatedUserId == requestedUserId)
        {
            context.Succeed(requirement);
            return;
        }

        // 2. Check friendship and privacy settings
        // This is where you query your domain logic/database
        // Example: Check if requestedUserId's friends list is public OR
        //          if authenticatedUserId and requestedUserId are friends AND
        //          if requestedUserId allows friends to see their friends list.
        bool canViewFriends = await _socialService.CanUserViewFriendsOf(
            authenticatedUserId,
            requestedUserId.Value,
            requestedFriendId.GetValueOrDefault());

        if (canViewFriends)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
