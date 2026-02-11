using Contracts.Models;
using Contracts.Responses;

namespace FeatureFlags.Client;

public interface IFeatureFlagsClient
{
    /// <summary>
    ///     Evaluates a feature flag and returns the full response, including the reason.
    /// </summary>
    /// <param name="featureFlagKey">The unique key of the feature flag.</param>
    /// <param name="context">Optional context (User ID or Groups) for targeting rules.</param>
    /// <param name="requestOptions">Optional headers or custom settings for this request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation response containing whether it is allowed and why.</returns>
    Task<EvaluationResponse> EvaluateAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a feature flag is enabled. Returns a simple boolean.
    /// </summary>
    /// <param name="featureFlagKey">The unique key of the feature flag.</param>
    /// <param name="context">Optional context (User ID or Groups) for targeting rules.</param>
    /// <param name="requestOptions">Optional headers or custom settings for this request.</param>
    /// <param name="defaultValue">Value to return if the flag is not found or an error occurs (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the flag is allowed, otherwise false.</returns>
    Task<bool> IsEnabledAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        bool defaultValue = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts listening for real-time feature flag updates via SignalR.
    /// </summary>
    /// <param name="projectId">
    ///     The optional project ID to subscribe to. If null, the server will attempt to determine it from the authentication
    ///     context via API Key.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StartListeningAsync(Guid? projectId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Occurs when a feature flag is updated on the server.
    /// </summary>
    event Action<string>? OnFlagChanged;
}