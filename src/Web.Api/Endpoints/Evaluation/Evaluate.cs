using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Evaluation;

public class Evaluate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation/{featureFlagKey}",
                async (string featureFlagKey,
                    string? userId,
                    string? email,
                    string? groups,
                    string? tenantId,
                    string? environment,
                    IEvaluationService evaluationService,
                    ClaimsPrincipal user,
                    ILogger<Evaluate> logger) =>
                {
                    try
                    {
                        var projectId = user.GetProjectId();
                        if (projectId == null)
                        {
                            logger.LogWarning("Evaluation request missing projectId claim");
                            return Results.Unauthorized();
                        }

                        // Build evaluation context from query parameters (client provides user context)
                        var context = new EvaluationContext(
                            userId ?? "anonymous",
                            email ?? "",
                            groups?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                            tenantId,
                            environment
                        );

                        logger.LogInformation(
                            "Evaluating feature flag: {Key} for user: {UserId} in project: {ProjectId}",
                            featureFlagKey, context.UserId, projectId);

                        var result = await evaluationService.EvaluateAsync(projectId.Value, featureFlagKey, context);
                        return Results.Ok(result);
                    }
                    catch (NotFoundException ex)
                    {
                        logger.LogError(ex, "Feature flag by key does not exist: {Key}", featureFlagKey);
                        return Results.NotFound(ex.Message);
                    }
                    catch (BadRequestException ex)
                    {
                        logger.LogError(ex, "An error occurred while evaluating feature flag by key: {Key}",
                            featureFlagKey);
                        return Results.BadRequest(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred while evaluating feature flag by key: {Key}",
                            featureFlagKey);
                        return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                    }
                })
            .RequireAuthorization("EvaluateAccess");
    }
}