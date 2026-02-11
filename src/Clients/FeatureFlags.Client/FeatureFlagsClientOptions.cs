namespace FeatureFlags.Client;

public sealed class FeatureFlagsClientOptions
{
    public Uri? BaseAddress { get; set; }
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }

    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public Version? ApiVersion { get; set; } = null;

    public TimeSpan? Timeout { get; set; } = null;

    public string? UserAgent { get; set; }

    public IDictionary<string, string> DefaultHeaders { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool EnableEvaluationCache { get; set; } = false;
    public TimeSpan EvaluationCacheDuration { get; set; } = TimeSpan.FromSeconds(30);

    public bool EnableEtagCaching { get; set; } = true;

    public bool EnableRetries { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(2);
    public bool RetryOnlyIdempotentRequests { get; set; } = true;
}