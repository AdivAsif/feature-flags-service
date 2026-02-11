using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Evaluation;

public class Evaluate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation/{featureFlagKey}",
                async (string featureFlagKey, IEvaluationService evaluationService, ClaimsPrincipal user,
                    ILogger<Evaluate> logger) =>
                {
                    try
                    {
                        var context = user.ToEvaluationContext();
                        logger.LogInformation("Evaluating feature flag: {Key} for user: {UserId}", featureFlagKey,
                            context.UserId);

                        var result = await evaluationService.EvaluateAsync(featureFlagKey, context);
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
            .RequireAuthorization();
    }
}