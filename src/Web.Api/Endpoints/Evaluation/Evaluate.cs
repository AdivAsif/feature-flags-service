using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using Web.Api.Extensions;
using Web.Api.JsonContexts;

namespace Web.Api.Endpoints.Evaluation;

public class Evaluate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation/{featureFlagKey}",
                async (string featureFlagKey,
                    string? userId,
                    string? groups,
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
                            groups?
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(g => g.Trim())
                                .Where(g => !string.IsNullOrWhiteSpace(g))
                                .Select(g => g.ToUpperInvariant())
                                .Distinct(StringComparer.Ordinal)
                                .ToList() ?? []
                        );

                        // logger.LogDebug(
                        //     "Evaluating feature flag: {Key} for user: {UserId} in project: {ProjectId}",
                        //     featureFlagKey, context.UserId, projectId);

                        var result = await evaluationService.EvaluateAsync(projectId.Value, featureFlagKey, context);
                        return Results.Json(result, ApiJsonContext.Default.EvaluationResultDto);
                    }
                    catch (NotFoundException ex)
                    {
                        // logger.LogError(ex, "Feature flag by key does not exist: {Key}", featureFlagKey);
                        return Results.NotFound(ex.Message);
                    }
                    catch (BadRequestException ex)
                    {
                        // logger.LogError(ex, "An error occurred while evaluating feature flag by key: {Key}", featureFlagKey);
                        return Results.BadRequest(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        // logger.LogError(ex, "An error occurred while evaluating feature flag by key: {Key}", featureFlagKey);
                        return Results.Problem(statusCode: 500, detail: "An unexpected error occurred: " + ex.Message);
                    }
                })
            .WithMetadata(new DisableETagMetadata())
            .RequireAuthorization("EvaluateAccess");
    }
}