using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.Projects;

public class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/projects/{id:guid}",
            async (Guid id, IProjectService projectService, ILogger<Delete> logger) =>
            {
                try
                {
                    await projectService.DeleteAsync(id);

                    return Results.NoContent();
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project not found with id: {Id}", id);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while deleting project with id: {Id}", id);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}