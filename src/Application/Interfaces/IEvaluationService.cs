using Application.DTOs;
using Domain;

namespace Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationResultDto> EvaluateAsync(Guid projectId, string featureFlagKey, EvaluationContext context,
        CancellationToken cancellationToken = default);
}