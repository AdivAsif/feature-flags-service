namespace FeatureFlags.Client;

public interface IFeatureFlagsClient
{
    Task<EvaluationResult> EvaluateAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        bool defaultValue = false,
        CancellationToken cancellationToken = default);
}