using Application.Interfaces;

namespace Web.Api.Endpoints.Projects;

public class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/projects", async (
                IProjectService projectService,
                ILogger<GetAll> logger) =>
            {
                logger.LogInformation("Getting all projects");

                var projects = await projectService.GetAllAsync();

                return Results.Ok(projects);
            })
            .WithName("GetAllProjects")
            .RequireAuthorization("User");
    }
}