using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Contracts.Requests;
using Microsoft.AspNetCore.SignalR;
using Web.Api.Extensions;
using Web.Api.Hubs;

namespace Web.Api.Endpoints.FeatureFlags;

public class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/feature-flags/{key}",
            async (string key, UpdateFeatureFlagRequest featureFlag, HttpContext httpContext, ClaimsPrincipal user,
                IFeatureFlagsService featureFlagsService, IHubContext<FeatureFlagHub> hubContext,
                ILogger<Update> logger) =>
            {
                try
                {
                    var projectId = user.GetProjectId();
                    if (projectId == null)
                    {
                        logger.LogWarning("Request missing projectId claim");
                        return Results.Unauthorized();
                    }

                    logger.LogDebug("Updating feature flag with key: {Key} for project: {ProjectId}", key,
                        projectId);

                    // Get the current feature flag to validate ETag
                    var currentFeatureFlag = await featureFlagsService.GetByKeyAsync(projectId.Value, key);
                    if (currentFeatureFlag == null) return Results.NotFound($"Feature flag with key: {key} not found");

                    // Validate If-Match header if present
                    if (httpContext.Request.HasIfMatchHeader())
                    {
                        var expectedETag = currentFeatureFlag.GenerateETag();
                        if (!httpContext.Request.ValidateETag(expectedETag))
                        {
                            logger.LogWarning("ETag mismatch for feature flag with key: {Key}", key);
                            return Results.StatusCode(412); // Precondition Failed
                        }
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

                    var updatedFeatureFlag =
                        await featureFlagsService.UpdateAsync(projectId.Value, key, featureFlag, performedByUserId,
                            performedByUserEmail);

                    await hubContext.Clients.Group(projectId.Value.ToString()).SendAsync("FlagChanged", key);

                    // Set ETag for the updated resource
                    var newETag = updatedFeatureFlag.GenerateETag();
                    httpContext.Response.Headers.ETag = newETag;

                    return Results.Ok(updatedFeatureFlag);
                }
                catch (NotFoundException ex)
                {
                    logger.LogError(ex, "Feature flag by key does not exist: {Key}", key);
                    return Results.NotFound(ex.Message);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while updating feature flag by key: {Key}", key);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while updating feature flag by key: {Key}", key);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("WriteAccess");
    }
}