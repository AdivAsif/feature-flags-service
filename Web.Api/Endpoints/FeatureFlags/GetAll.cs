using Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Api.Endpoints.FeatureFlags;

public class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/feature-flags", async (IFeatureFlagsService featureFlagsService, ILogger<GetAll> logger) =>
            {
                logger.LogInformation("Getting all feature flags");
                var allFeatureFlags = await featureFlagsService.GetAllAsync();
                return Results.Ok(allFeatureFlags);
            })
            .WithName("GetAllFeatureFlags")
            .RequireAuthorization("ReadAccess");
    }
}