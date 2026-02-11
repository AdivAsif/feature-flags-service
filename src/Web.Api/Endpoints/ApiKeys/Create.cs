using System.Security.Claims;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.ApiKeys;

public class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/projects/{projectId:guid}/apikeys",
            async (Guid projectId, CreateApiKeyDTO createApiKeyDto, HttpContext httpContext,
                IApiKeyService apiKeyService, ILogger<Create> logger) =>
            {
                try
                {
                    logger.LogInformation("Creating API key for project: {ProjectId} with name: {Name}",
                        projectId, createApiKeyDto.Name);

                    var createdByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                          httpContext.User.FindFirstValue("sub") ??
                                          "unknown";

                    var createdApiKey = await apiKeyService.CreateAsync(projectId, createApiKeyDto, createdByUserId);

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