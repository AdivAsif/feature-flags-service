namespace FeatureFlags.Client;

/// <summary>
///     Factory for creating <see cref="FeatureFlagsClient" /> instances.
///     Useful if you are not using Dependency Injection.
/// </summary>
public static class FeatureFlagsClientFactory
{
    /// <summary>
    ///     Creates a new client with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    /// <returns>A new <see cref="FeatureFlagsClient" />.</returns>
    public static FeatureFlagsClient Create(FeatureFlagsClientOptions options)
    {
        return new FeatureFlagsClient(new HttpClient(), options);
    }

    /// <summary>
    ///     Creates a new client with options and a custom HTTP handler.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    /// <param name="handler">Custom HTTP message handler (e.g. for mocking or custom middleware).</param>
    /// <returns>A new <see cref="FeatureFlagsClient" />.</returns>
    public static FeatureFlagsClient Create(FeatureFlagsClientOptions options, HttpMessageHandler handler)
    {
        return new FeatureFlagsClient(new HttpClient(handler), options);
    }
}