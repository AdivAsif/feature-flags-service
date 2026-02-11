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
            async (FeatureFlagDTO featureFlag, HttpContext httpContext, IFeatureFlagsService featureFlagsService,
                ILogger<Create> logger) =>
            {
                try
                {
                    logger.LogInformation("Creating feature flag with key: {Key}", featureFlag.Key);
                    var createdFeatureFlag = await featureFlagsService.CreateAsync(featureFlag);

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