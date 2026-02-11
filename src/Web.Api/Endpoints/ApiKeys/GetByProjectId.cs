using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.ApiKeys;

public class GetByProjectId : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/projects/{projectId:guid}/apikeys", async (
                Guid projectId,
                IApiKeyService apiKeyService,
                ILogger<GetByProjectId> logger) =>
            {
                try
                {
                    logger.LogDebug("Getting API keys for project: {ProjectId}", projectId);

                    var apiKeys = await apiKeyService.GetByProjectIdAsync(projectId);

                    return Results.Ok(apiKeys);
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project not found with id: {ProjectId}", projectId);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while getting API keys for project: {ProjectId}", projectId);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            })
            .WithName("GetApiKeysByProjectId")
            .RequireAuthorization("User");
    }
}