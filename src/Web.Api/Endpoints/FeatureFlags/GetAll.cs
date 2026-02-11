using System.Security.Claims;
using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.FeatureFlags;

public class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/feature-flags", async (
                IFeatureFlagsService featureFlagsService,
                ClaimsPrincipal user,
                ILogger<GetAll> logger,
                int first = 10,
                string? after = null,
                string? before = null) =>
            {
                var projectId = user.GetProjectId();
                if (projectId == null)
                {
                    logger.LogWarning("Request missing projectId claim");
                    return Results.Unauthorized();
                }

                logger.LogInformation(
                    "Getting feature flags for project {ProjectId} with cursor pagination (first: {First}, after: {After}, before: {Before})",
                    projectId, first, after ?? "null", before ?? "null");

                var pagedResult = await featureFlagsService.GetPagedAsync(projectId.Value, first, after, before);

                return Results.Ok(pagedResult);
            })
            .WithName("GetAllFeatureFlags")
            .RequireAuthorization("ReadAccess");
    }
}