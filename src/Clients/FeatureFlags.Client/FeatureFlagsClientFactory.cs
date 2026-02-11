namespace FeatureFlags.Client;

public static class FeatureFlagsClientFactory
{
    public static FeatureFlagsClient Create(FeatureFlagsClientOptions options)
    {
        return new FeatureFlagsClient(new HttpClient(), options);
    }

    public static FeatureFlagsClient Create(FeatureFlagsClientOptions options, HttpMessageHandler handler)
    {
        return new FeatureFlagsClient(new HttpClient(handler), options);
    }
}