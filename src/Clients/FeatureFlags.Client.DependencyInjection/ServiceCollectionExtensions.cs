using System.Globalization;
using FeatureFlags.Client.DependencyInjection.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeatureFlags.Client.DependencyInjection;

/// <summary>
///     Extension methods for setting up the Feature Flags client in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static void ValidateOptions(FeatureFlagsClientOptions options)
    {
        if (options.BaseAddress is null)
            throw new InvalidOperationException("Feature flags client options are missing BaseAddress.");

        if (!options.BaseAddress.IsAbsoluteUri)
            throw new InvalidOperationException("Feature flags client options BaseAddress must be an absolute URI.");

        if (string.IsNullOrWhiteSpace(options.ApiKey) && string.IsNullOrWhiteSpace(options.BearerToken))
            throw new InvalidOperationException("Feature flags client options are missing ApiKey or BearerToken.");

        if (string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
            throw new InvalidOperationException("Feature flags client options are missing ApiKeyHeaderName.");
    }

    private static TimeSpan? ParseDurationSeconds(string? seconds)
    {
        if (string.IsNullOrWhiteSpace(seconds))
            return null;

        if (!double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        return TimeSpan.FromSeconds(value);
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Adds the Feature Flags client to the service collection.
        /// </summary>
        /// <param name="configure">A delegate to configure the <see cref="FeatureFlagsClientOptions" />.</param>
        /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
        public IServiceCollection AddFeatureFlagsClient(Action<FeatureFlagsClientOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new FeatureFlagsClientOptions();
            configure(options);

            return services.AddFeatureFlagsClient(options);
        }

        /// <summary>
        ///     Adds the Feature Flags client using configuration from <see cref="IConfiguration" />.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <param name="sectionName">The name of the configuration section (default: "FeatureFlags").</param>
        /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
        public IServiceCollection AddFeatureFlagsClient(IConfiguration configuration,
            string sectionName = "FeatureFlags")
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return services.AddFeatureFlagsClient(configuration.GetSection(sectionName));
        }

        /// <summary>
        ///     Adds the Feature Flags client using a specific <see cref="IConfigurationSection" />.
        /// </summary>
        /// <param name="section">The configuration section containing client settings.</param>
        /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
        public IServiceCollection AddFeatureFlagsClient(IConfigurationSection section)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(section);

            var baseAddressString = section["BaseAddress"] ?? section["BaseUrl"];
            var apiKey = section["ApiKey"];
            var bearerToken = section["BearerToken"];

            if (string.IsNullOrWhiteSpace(baseAddressString))
                throw new InvalidOperationException("Feature flags client configuration is missing 'BaseAddress'.");

            if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(bearerToken))
                throw new InvalidOperationException(
                    "Feature flags client configuration is missing either 'ApiKey' or 'BearerToken'.");

            Version? apiVersion = null;
            var apiVersionString = section["ApiVersion"];
            if (!string.IsNullOrWhiteSpace(apiVersionString))
                apiVersion = Version.Parse(apiVersionString);

            TimeSpan? timeout = null;
            var timeoutString = section["TimeoutSeconds"];
            if (!string.IsNullOrWhiteSpace(timeoutString) &&
                double.TryParse(timeoutString, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var timeoutSeconds))
                timeout = TimeSpan.FromSeconds(timeoutSeconds);

            return services.AddFeatureFlagsClient(new FeatureFlagsClientOptions
            {
                BaseAddress = new Uri(baseAddressString, UriKind.Absolute),
                ApiKey = apiKey,
                BearerToken = bearerToken,
                ApiKeyHeaderName = section["ApiKeyHeaderName"] ?? "X-Api-Key",
                ApiVersion = apiVersion,
                Timeout = timeout,
                UserAgent = section["UserAgent"],
                EnableEvaluationCache =
                    bool.TryParse(section["EnableEvaluationCache"], out var enableEvaluationCache) &&
                    enableEvaluationCache,
                EvaluationCacheDuration =
                    ParseDurationSeconds(section["EvaluationCacheSeconds"]) ?? TimeSpan.FromSeconds(30),
                EnableEtagCaching = !bool.TryParse(section["EnableEtagCaching"], out var enableEtagCaching) ||
                                    enableEtagCaching,
                EnableRetries = !bool.TryParse(section["EnableRetries"], out var enableRetries) || enableRetries,
                MaxRetries = int.TryParse(section["MaxRetries"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var maxRetries)
                    ? maxRetries
                    : 3,
                RetryBaseDelay = ParseDurationSeconds(section["RetryBaseDelaySeconds"]) ??
                                 TimeSpan.FromMilliseconds(200),
                RetryMaxDelay = ParseDurationSeconds(section["RetryMaxDelaySeconds"]) ?? TimeSpan.FromSeconds(2),
                RetryOnlyIdempotentRequests =
                    !bool.TryParse(section["RetryOnlyIdempotentRequests"], out var retryIdempotent) || retryIdempotent
            });
        }

        /// <summary>
        ///     Adds the Feature Flags client with pre-configured options.
        /// </summary>
        /// <param name="options">The configured <see cref="FeatureFlagsClientOptions" />.</param>
        /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
        public IServiceCollection AddFeatureFlagsClient(FeatureFlagsClientOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            ValidateOptions(options);

            services.AddSingleton(options);

            services.AddHttpClient("FeatureFlags", (sp, httpClient) =>
            {
                var resolvedOptions = sp.GetRequiredService<FeatureFlagsClientOptions>();
                httpClient.BaseAddress = resolvedOptions.BaseAddress;
                if (resolvedOptions.Timeout is not null)
                    httpClient.Timeout = resolvedOptions.Timeout.Value;
            }).AddHttpMessageHandler(sp =>
            {
                var resolvedOptions = sp.GetRequiredService<FeatureFlagsClientOptions>();
                var logger = sp.GetService<ILogger<FeatureFlagsRetryHandler>>();
                return new FeatureFlagsRetryHandler(resolvedOptions, logger);
            });

            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("FeatureFlags");
                var resolvedOptions = sp.GetRequiredService<FeatureFlagsClientOptions>();
                return new FeatureFlagsClient(httpClient, resolvedOptions);
            });

            services.AddSingleton<IFeatureFlagsClient>(sp => sp.GetRequiredService<FeatureFlagsClient>());
            services.AddSingleton<IFeatureFlagsManagementClient>(sp => sp.GetRequiredService<FeatureFlagsClient>());

            return services;
        }
    }
}