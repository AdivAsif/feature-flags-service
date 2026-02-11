using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.Projects;

public class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/projects/{id:guid}",
            async (Guid id, UpdateProjectDto updateProjectDto, IProjectService projectService,
                ILogger<Update> logger) =>
            {
                try
                {
                    logger.LogDebug("Updating project with id: {Id}", id);

                    var updatedProject = await projectService.UpdateAsync(id, updateProjectDto);

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