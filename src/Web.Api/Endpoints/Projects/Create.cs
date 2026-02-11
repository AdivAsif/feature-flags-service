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
            async (CreateProjectDto createProjectDto, HttpContext httpContext, IProjectService projectService,
                ILogger<Create> logger) =>
            {
                try
                {
                    // Debug: Log all claims
                    var claims = httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}");
                    logger.LogDebug("User claims: {Claims}", string.Join(", ", claims));
                    logger.LogDebug("User.Identity.IsAuthenticated: {IsAuthenticated}",
                        httpContext.User.Identity?.IsAuthenticated);

                    logger.LogDebug("Creating project with name: {Name}", createProjectDto.Name);
                    var performedByUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                            httpContext.User.FindFirstValue("sub");

                    var result = await projectService.CreateAsync(createProjectDto, performedByUserId);

                    return result.Created
                        ? Results.CreatedAtRoute("GetProjectById", new { id = result.Project.Id }, result.Project)
                        : Results.Ok(result.Project);
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