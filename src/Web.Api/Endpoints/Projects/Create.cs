using System.Security.Claims;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.Projects;

public class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/projects",
            async (CreateProjectDTO createProjectDto, HttpContext httpContext, IProjectService projectService,
                ILogger<Create> logger) =>
            {
                try
                {
                    // Debug: Log all claims
                    var claims = httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}");
                    logger.LogInformation("User claims: {Claims}", string.Join(", ", claims));
                    logger.LogInformation("User.Identity.IsAuthenticated: {IsAuthenticated}",
                        httpContext.User.Identity?.IsAuthenticated);

                    logger.LogInformation("Creating project with name: {Name}", createProjectDto.Name);
                    var performedByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                            httpContext.User.FindFirstValue("sub");

                    var createdProject = await projectService.CreateAsync(createProjectDto, performedByUserId);

                    return Results.CreatedAtRoute("GetProjectById", new { id = createdProject.Id },
                        createdProject);
                }
                catch (BadRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while creating project with name: {Name}",
                        createProjectDto.Name);
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while creating project with name: {Name}",
                        createProjectDto.Name);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("Admin");
    }
}