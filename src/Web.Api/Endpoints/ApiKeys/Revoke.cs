using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.ApiKeys;

public class Revoke : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/projects/{projectId:guid}/apikeys/{keyId:guid}",
            async (Guid projectId, Guid keyId, IApiKeyService apiKeyService, ILogger<Revoke> logger) =>
            {
                try
                {
                    logger.LogDebug("Revoking API key: {KeyId} for project: {ProjectId}", keyId, projectId);

                    await apiKeyService.RevokeAsync(projectId, keyId);

                    return Results.NoContent();
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project or API key not found - ProjectId: {ProjectId}, KeyId: {KeyId}",
                        projectId, keyId);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while revoking API key: {KeyId} for project: {ProjectId}",
                        keyId, projectId);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}