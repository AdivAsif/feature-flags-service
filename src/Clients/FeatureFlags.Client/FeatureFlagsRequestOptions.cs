namespace FeatureFlags.Client;

public sealed class FeatureFlagsRequestOptions
{
    public IDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}