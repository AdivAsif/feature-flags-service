using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.Projects;

public class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/projects/{id:guid}", async (
                Guid id,
                IProjectService projectService,
                ILogger<GetById> logger) =>
            {
                try
                {
                    var project = await projectService.GetByIdAsync(id);

                    return Results.Ok(project);
                }
                catch (NotFoundException ex)
                {
                    logger.LogWarning(ex, "Project not found with id: {Id}", id);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while getting project by id: {Id}", id);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            })
            .WithName("GetProjectById")
            .RequireAuthorization("User");
    }
}