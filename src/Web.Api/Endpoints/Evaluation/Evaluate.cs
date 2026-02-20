using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Contracts.Models;
using Contracts.Responses;
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
                        var context = new EvaluationContext
                        {
                            UserId = userId ?? "anonymous",
                            Groups = groups is null ? [] : ParseGroups(groups)
                        };

                        var result = await evaluationService.EvaluateAsync(projectId.Value, featureFlagKey, context);
                        return Results.Json(result, ApiJsonContext.Default.EvaluationResponse);
                    }
                    catch (NotFoundException ex)
                    {
                        return Results.NotFound(ex.Message);
                    }
                    catch (BadRequestException ex)
                    {
                        return Results.BadRequest(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(statusCode: 500, detail: "An unexpected error occurred: " + ex.Message);
                    }
                })
            .WithMetadata(new DisableETagMetadata())
            .RequireAuthorization("EvaluateAccess")
            .Produces<EvaluationResponse>();
    }

    private static List<string> ParseGroups(string groups)
    {
        if (string.IsNullOrWhiteSpace(groups)) return [];

        // Initial capacity of 4 to handle most scenarios without resizing
        var result = new List<string>(4);
        var span = groups.AsSpan();

        while (!span.IsEmpty)
        {
            var commaIndex = span.IndexOf(',');
            ReadOnlySpan<char> groupSpan;

            if (commaIndex < 0)
            {
                groupSpan = span;
                span = ReadOnlySpan<char>.Empty;
            }
            else
            {
                groupSpan = span[..commaIndex];
                span = span[(commaIndex + 1)..];
            }

            // Trim whitespace manually to avoid allocation
            groupSpan = groupSpan.Trim();
            if (groupSpan.IsEmpty) continue;
            result.Add(groupSpan.ToString());
        }

        return result;
    }
}