namespace FeatureFlags.Client;

/// <summary>
///     Options for customizing a specific feature flags request.
/// </summary>
public sealed class FeatureFlagsRequestOptions
{
    /// <summary>
    ///     Custom headers to send with this specific request.
    /// </summary>
    public IDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}