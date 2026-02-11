using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Contracts.Requests;

namespace Web.Api.Endpoints.ApiKeys;

public class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/projects/{projectId:guid}/apikeys",
            async (Guid projectId, CreateApiKeyRequest CreateApiKeyRequest, HttpContext httpContext,
                IApiKeyService apiKeyService, ILogger<Create> logger) =>
            {
                try
                {
                    logger.LogDebug("Creating API key for project: {ProjectId} with name: {Name}",
                        projectId, CreateApiKeyRequest.Name);

                    var createdByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                          httpContext.User.FindFirstValue("sub") ??
                                          "unknown";

                    var createdApiKey =
                        await apiKeyService.CreateAsync(projectId, CreateApiKeyRequest, createdByUserId);

                    return Results.Ok(createdApiKey);
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project not found with id: {ProjectId}", projectId);
                    return Results.NotFound(ex.Message);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while creating API key for project: {ProjectId}", projectId);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while creating API key for project: {ProjectId}", projectId);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}