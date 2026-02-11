using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.FeatureFlags;

public class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/feature-flags/{key}",
            async (string key, IFeatureFlagsService featureFlagsService, ILogger<Delete> logger) =>
            {
                try
                {
                    logger.LogInformation("Deleting feature flag with key: {Key}", key);
                    await featureFlagsService.DeleteByKeyAsync(key);
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