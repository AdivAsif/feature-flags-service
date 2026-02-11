using Application.Interfaces;

namespace Web.Api.Endpoints.FeatureFlags;

public class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/feature-flags", async (
                IFeatureFlagsService featureFlagsService,
                ILogger<GetAll> logger,
                int first = 10,
                string? after = null,
                string? before = null) =>
            {
                logger.LogInformation(
                    "Getting feature flags with cursor pagination (first: {First}, after: {After}, before: {Before})",
                    first, after ?? "null", before ?? "null");

                var pagedResult = await featureFlagsService.GetPagedAsync(first, after, before);

                return Results.Ok(pagedResult);
            })
            .WithName("GetAllFeatureFlags")
            .RequireAuthorization("ReadAccess");
    }
}