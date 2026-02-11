using System.Security.Claims;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.FeatureFlags;

public class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/feature-flags",
            async (FeatureFlagDto featureFlag, HttpContext httpContext, ClaimsPrincipal user,
                IFeatureFlagsService featureFlagsService,
                ILogger<Create> logger) =>
            {
                try
                {
                    var projectId = user.GetProjectId();
                    if (projectId == null)
                    {
                        logger.LogWarning("Request missing projectId claim");
                        return Results.Unauthorized();
                    }

                    logger.LogDebug("Creating feature flag with key: {Key} for project: {ProjectId}",
                        featureFlag.Key, projectId);
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

                    var createdFeatureFlag =
                        await featureFlagsService.CreateAsync(projectId.Value, featureFlag, performedByUserId,
                            performedByUserEmail);

                    // Set ETag for the created resource
                    var etag = createdFeatureFlag.GenerateETag();
                    httpContext.Response.Headers.ETag = etag;

                    return Results.CreatedAtRoute("GetFeatureFlagByKey", new { key = createdFeatureFlag.Key },
                        createdFeatureFlag);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while creating feature flag by key: {Key}", featureFlag.Key);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while creating feature flag by key: {Key}", featureFlag.Key);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("WriteAccess");
    }
}