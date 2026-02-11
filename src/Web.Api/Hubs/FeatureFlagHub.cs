using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Web.Api.Hubs;

[Authorize]
public class FeatureFlagHub(ILogger<FeatureFlagHub> logger) : Hub
{
    public async Task SubscribeToProject(Guid? projectId = null)
    {
        var finalProjectId = projectId;

        if (finalProjectId == null)
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var projectIdClaim = user.FindFirst("projectId");
                if (projectIdClaim != null && Guid.TryParse(projectIdClaim.Value, out var parsedId))
                    finalProjectId = parsedId;
                else
                    // If we are authenticated but missing the claim, it might be a different user type or configuration issue
                    logger.LogWarning("Authenticated user {UserIdentifier} missing projectId claim",
                        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            }
            else
            {
                logger.LogWarning("Unauthenticated user {ConnectionId} attempting to subscribe to project",
                    Context.ConnectionId);
            }
        }

        if (finalProjectId != null)
        {
            var groupId = finalProjectId.Value.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        }
        else
        {
            logger.LogWarning("Subscription failed for client {ConnectionId}: No project ID found",
                Context.ConnectionId);
            await Clients.Caller.SendAsync("SubscriptionFailed", "No project ID found in request or claims.");
        }
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            await SubscribeToProject();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("SubscriptionFailed",
                "An error occurred while subscribing to project updates.");
        }

        await base.OnConnectedAsync();
    }
}