using Application.Interfaces;
using Application.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Api.Endpoints.FeatureFlags;

public class GetByKey : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/feature-flags/{key}",
                async (string key, IFeatureFlagsService featureFlagsService, ILogger<GetByKey> logger) =>
                {
                    try
                    {
                        logger.LogInformation("Getting feature flag by key: {Key}", key);
                        var featureFlag = await featureFlagsService.GetByKeyAsync(key);
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
