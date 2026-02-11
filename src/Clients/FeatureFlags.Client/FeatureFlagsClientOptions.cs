namespace FeatureFlags.Client;

/// <summary>
///     Configuration options for the <see cref="FeatureFlagsClient" />.
/// </summary>
public sealed class FeatureFlagsClientOptions
{
    /// <summary>
    ///     The base URI of the Feature Flags service.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    ///     The API Key for authentication.
    ///     Must have a BearerToken to acquire an API key initially.
    /// </summary>
    public string? ApiKey { get; set; } // Must have a BearerToken to acquire an API key

    /// <summary>
    ///     The Bearer Token for authentication.
    ///     Acquired either through dev/token if a JwtSecretKey is used or through an external authentication provider like
    ///     Auth0.
    /// </summary>
    // Acquired either through dev/token if a JwtSecretKey is used or through an external authentication provider like Auth0
    public string? BearerToken { get; init; }

    /// <summary>
    ///     The HTTP header name used for sending the API Key. Default is "X-Api-Key".
    /// </summary>
    public string ApiKeyHeaderName { get; init; } = "X-Api-Key";

    /// <summary>
    ///     The specific API version to target.
    /// </summary>
    public Version? ApiVersion { get; set; }

    /// <summary>
    ///     The timeout for HTTP requests.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    ///     Custom User-Agent string to send with requests.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    ///     Default HTTP headers to be sent with every request.
    /// </summary>
    public IDictionary<string, string> DefaultHeaders { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Enables client-side caching of flag evaluations.
    ///     It already caches on the server side, so this is just an additional layer of caching to reduce latency and server
    ///     load for frequently evaluated flags on the client side.
    /// </summary>
    // It already caches on the server side, so this is just an additional layer of caching to reduce latency and server load for frequently evaluated flags on the client side
    public bool EnableEvaluationCache { get; init; }

    /// <summary>
    ///     The duration for which evaluation results are cached on the client. Default is 30 seconds.
    /// </summary>
    public TimeSpan EvaluationCacheDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Enables HTTP ETag caching for feature flag definitions. Default is true.
    /// </summary>
    public bool EnableEtagCaching { get; init; } = true;

    /// <summary>
    ///     Enables automatic retries for failed requests. Default is true.
    /// </summary>
    public bool EnableRetries { get; set; } = true;

    /// <summary>
    ///     The maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    ///     The initial delay between retries. Default is 200ms.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///     The maximum delay between retries. Default is 2 seconds.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Whether to retry only idempotent requests. Default is true.
    /// </summary>
    public bool RetryOnlyIdempotentRequests { get; init; } = true;
}