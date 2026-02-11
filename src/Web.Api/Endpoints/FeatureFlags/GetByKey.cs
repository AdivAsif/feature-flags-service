using Application.Exceptions;
using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.FeatureFlags;

public class GetByKey : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/feature-flags/{key}",
                async (string key, HttpContext httpContext, IFeatureFlagsService featureFlagsService,
                    ILogger<GetByKey> logger) =>
                {
                    try
                    {
                        logger.LogInformation("Getting feature flag by key: {Key}", key);
                        var featureFlag = await featureFlagsService.GetByKeyAsync(key);

                        if (featureFlag == null) return Results.NotFound($"Feature flag with key: {key} not found");

                        var etag = featureFlag.GenerateETag();
                        httpContext.Response.Headers.ETag = etag;

                        if (httpContext.Request.Headers.TryGetValue("If-None-Match", out var incomingEtag) &&
                            incomingEtag == etag)
                            return Results.StatusCode(304);

                        return Results.Ok(featureFlag);
                    }
                    catch (NotFoundException ex)
                    {
                        logger.LogError(ex, "Feature flag with key: {Key} not found", key);
                        return Results.NotFound(ex.Message);
                    }
                    catch (BadRequestException ex)
                    {
                        logger.LogError(ex, "An error occurred while getting feature flag by key: {Key}", key);
                        return Results.BadRequest(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred while getting feature flag by key: {Key}", key);
                        return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                    }
                })
            .WithName("GetFeatureFlagByKey")
            .RequireAuthorization("ReadAccess");
    }
}