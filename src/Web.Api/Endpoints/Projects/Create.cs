using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Contracts.Requests;

namespace Web.Api.Endpoints.Projects;

public class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/projects",
            async (CreateProjectRequest createProjectRequest, HttpContext httpContext, IProjectService projectService,
                ILogger<Create> logger) =>
            {
                try
                {
                    var performedByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                            httpContext.User.FindFirstValue("sub");

                    var result = await projectService.CreateAsync(createProjectRequest, performedByUserId);

                    return result.Created
                        ? Results.CreatedAtRoute("GetProjectById", new { id = result.Project.Id }, result.Project)
                        : Results.Ok(result.Project);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while creating project with name: {Name}",
                        createProjectRequest.Name);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while creating project with name: {Name}",
                        createProjectRequest.Name);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}