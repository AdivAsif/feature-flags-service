using Application.DTOs;
using Application.Interfaces;
using Application.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Api.Endpoints.FeatureFlags;

public class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/feature-flags/{key}",
            async (string key, FeatureFlagDTO featureFlag, IFeatureFlagsService featureFlagsService,
                ILogger<Update> logger) =>
            {
                try
                {
                    logger.LogInformation("Updating feature flag with key: {Key}", key);
                    var updatedFeatureFlag = await featureFlagsService.UpdateAsync(key, featureFlag);
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
