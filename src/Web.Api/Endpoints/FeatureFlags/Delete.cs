using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.FeatureFlags;

public class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/feature-flags/{key}",
            async (string key, HttpContext httpContext, ClaimsPrincipal user, IFeatureFlagsService featureFlagsService,
                ILogger<Delete> logger) =>
            {
                try
                {
                    var projectId = user.GetProjectId();
                    if (projectId == null)
                    {
                        logger.LogWarning("Request missing projectId claim");
                        return Results.Unauthorized();
                    }

                    var performedByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                            httpContext.User.FindFirstValue("sub");
                    var performedByUserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email) ??
                                               httpContext.User.FindFirstValue("email");

                    if (string.IsNullOrWhiteSpace(performedByUserId))
                        performedByUserId = httpContext.User.FindFirstValue("apiKeyId");

                    if (string.IsNullOrWhiteSpace(performedByUserEmail))
                    {
                        var apiKeyName = httpContext.User.FindFirstValue("apiKeyName");
                        var apiKeyId = httpContext.User.FindFirstValue("apiKeyId") ?? performedByUserId;
                        performedByUserEmail = !string.IsNullOrWhiteSpace(apiKeyName)
                            ? $"apiKey:{apiKeyName}"
                            : !string.IsNullOrWhiteSpace(apiKeyId)
                                ? $"apiKey:{apiKeyId}"
                                : null;
                    }

                    await featureFlagsService.DeleteByKeyAsync(projectId.Value, key, performedByUserId,
                        performedByUserEmail);
                    return Results.NoContent();
                }
                catch (NotFoundException ex)
                {
                    logger.LogError(ex, "Feature flag by key does not exist: {Key}", key);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while deleting feature flag by key: {Key}", key);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("DeleteAccess");
    }
}