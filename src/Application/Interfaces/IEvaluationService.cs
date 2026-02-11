using Contracts.Models;
using Contracts.Responses;
using Domain;

namespace Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationResponse> EvaluateAsync(Guid projectId, string featureFlagKey, EvaluationContext context,
        CancellationToken cancellationToken = default);

    Task<EvaluationResponse> EvaluateAsync(FeatureFlag featureFlag, EvaluationContext context,
        CancellationToken cancellationToken = default);
}