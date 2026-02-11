using Application.Exceptions;
using Application.Interfaces;
using Contracts.Requests;

namespace Web.Api.Endpoints.Projects;

public class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/projects/{id:guid}",
            async (Guid id, UpdateProjectRequest updateProjectRequest, IProjectService projectService,
                ILogger<Update> logger) =>
            {
                try
                {
                    var updatedProject = await projectService.UpdateAsync(id, updateProjectRequest);

                    return Results.Ok(updatedProject);
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project not found with id: {Id}", id);
                    return Results.NotFound(ex.Message);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while updating project with id: {Id}", id);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while updating project with id: {Id}", id);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}