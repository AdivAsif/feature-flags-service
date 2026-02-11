using System.Net;
using System.Text;
using Contracts.Common;
using Contracts.Models;
using FeatureFlags.Client.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlags.Client.Tests;

public sealed class FeatureFlagsClientTests
{
    [Fact]
    public async Task EvaluateAsync_UsesBearerAuth_WhenApiKeyLooksLikeApiKey()
    {
        // Arrange
        HttpRequestMessage? captured = null;

        var handler = new RecordingHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true,"reason":"ok"}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123" // As long as the prefix looks like an API key's would, it knows it is present
        });

        // Act
        _ = await client.EvaluateAsync("my-flag");

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("ffsk_123", captured.Headers.Authorization?.Parameter);
        Assert.False(captured.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task EvaluateAsync_UsesHeaderAuth_WhenApiKeyIsArbitrary()
    {
        // Arrange
        HttpRequestMessage? captured = null;

        var handler = new RecordingHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "my-key", // If the API key doesn't look like a real API key, it falls back to sending it as a header with the configured header name
            ApiKeyHeaderName = "X-Api-Key"
        });

        // Act
        _ = await client.EvaluateAsync("my-flag");

        // Assert
        Assert.NotNull(captured);
        Assert.Null(captured!.Headers.Authorization);
        Assert.True(captured.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("my-key", values.Single());
    }

    [Fact]
    public async Task EvaluateAsync_AddsApiVersionQuery_WhenConfigured()
    {
        // Arrange
        Uri? capturedUri = null;

        var handler = new RecordingHandler((req, _) =>
        {
            capturedUri = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123",
            ApiVersion = new Version(1, 0)
        });

        // Act
        _ = await client.EvaluateAsync("my-flag");

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("api-version=1.0", capturedUri!.Query);
    }

    [Fact]
    public async Task EvaluateAsync_HasContextQueryParameters()
    {
        // Arrange
        Uri? capturedUri = null;

        var handler = new RecordingHandler((req, _) =>
        {
            capturedUri = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123"
        });

        // Act
        _ = await client.EvaluateAsync("my-flag", new EvaluationContext
        {
            UserId = "u1",
            Groups = ["beta", "internal"]
            // TenantId = "t1",
            // Environment = "dev"
        });

        // Assert
        Assert.NotNull(capturedUri);
        var query = capturedUri!.Query;
        Assert.Contains("userId=u1", query);
        // Assert.Contains("email=a%40b.com", query);
        Assert.Contains("groups=beta%2Cinternal", query);
        // Assert.Contains("tenantId=t1", query);
        // Assert.Contains("environment=dev", query);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsFeatureFlagsApiException_OnUnauthorized()
    {
        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized", Encoding.UTF8, "text/plain")
            }));

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123"
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<FeatureFlagsApiException>(() => client.EvaluateAsync("my-flag"));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("unauthorized", ex.ResponseBody);
    }

    [Fact]
    public async Task EvaluateAsync_ParsesProblemDetails_WhenReturned()
    {
        // Arrange
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"type":"about:blank","title":"Bad Request","status":400,"detail":"errors","traceId":"t1"}""",
                    Encoding.UTF8,
                    "application/problem+json")
            };
            return Task.FromResult(response);
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123"
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<FeatureFlagsApiException>(() => client.EvaluateAsync("my-flag"));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.NotNull(ex.ProblemDetails);
        Assert.Equal("errors", ex.ProblemDetails!.Detail);
        Assert.Equal("t1", ex.ProblemDetails.TraceId);
    }

    [Fact]
    public async Task EvaluateAsync_AddsDefaultHeaders_AndPerRequestHeaders()
    {
        // Arrange
        HttpRequestMessage? captured = null;

        var handler = new RecordingHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123",
            DefaultHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Correlation-Id"] = "c1"
            }
        });

        var requestOptions = new FeatureFlagsRequestOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Request-Id"] = "r1"
            }
        };

        // Act
        _ = await client.EvaluateAsync("my-flag", requestOptions: requestOptions);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("X-Correlation-Id", out var correlation));
        Assert.Equal("c1", correlation.Single());
        Assert.True(captured.Headers.TryGetValues("X-Request-Id", out var requestId));
        Assert.Equal("r1", requestId.Single());
        Assert.NotEmpty(captured.Headers.UserAgent);
    }

    [Fact]
    public async Task EvaluateAsync_CachesResult_WhenEnabled()
    {
        // Arrange
        var calls = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123",
            EnableEvaluationCache = true,
            EvaluationCacheDuration = TimeSpan.FromMinutes(1)
        });

        var context = new EvaluationContext { UserId = "u1" };

        // Act
        _ = await client.EvaluateAsync("my-flag", context);
        _ = await client.EvaluateAsync("my-flag", context);

        // Assert
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsDefaultValue_OnNotFound()
    {
        // Arrange
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("missing", Encoding.UTF8, "text/plain")
        }));

        var client = new FeatureFlagsClient(new HttpClient(handler), new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri("https://example.test/api/"),
            ApiKey = "ffsk_123"
        });

        // Act
        var enabled = await client.IsEnabledAsync("missing-flag", defaultValue: true);

        // Assert
        Assert.True(enabled);
    }

    [Fact]
    public async Task DependencyInjection_RegistersClient()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddFeatureFlagsClient(options =>
        {
            options.BaseAddress = new Uri("https://example.test/api/");
            options.ApiKey = "ffsk_123";
            options.ApiVersion = new Version(1, 0);
        });

        // Act
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IFeatureFlagsClient>();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task DependencyInjection_BindsFromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:BaseAddress"] = "https://example.test/api/",
                ["FeatureFlags:ApiKey"] = "ffsk_123",
                ["FeatureFlags:ApiVersion"] = "1.0",
                ["FeatureFlags:TimeoutSeconds"] = "2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFeatureFlagsClient(configuration);

        // Act
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FeatureFlagsClientOptions>();

        // Assert
        Assert.Equal(new Uri("https://example.test/api/"), options.BaseAddress);
        Assert.Equal("ffsk_123", options.ApiKey);
        Assert.Equal(new Version(1, 0), options.ApiVersion);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Timeout);
    }

    [Fact]
    public async Task DependencyInjection_RetriesTransientFailures_WhenEnabled()
    {
        // Arrange
        var calls = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            calls++;

            if (calls == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("fail", Encoding.UTF8, "text/plain")
                });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"allowed":true}""", Encoding.UTF8, "application/json")
            });
        });

        var services = new ServiceCollection();
        services.AddFeatureFlagsClient(options =>
        {
            options.BaseAddress = new Uri("https://example.test/api/");
            options.ApiKey = "ffsk_123";
            options.EnableRetries = true;
            options.MaxRetries = 2;
            options.RetryBaseDelay = TimeSpan.Zero;
            options.RetryMaxDelay = TimeSpan.Zero;
        });

        services.AddHttpClient("FeatureFlags")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IFeatureFlagsClient>();

        // Act
        _ = await client.EvaluateAsync("my-flag");

        // Assert
        Assert.Equal(2, calls);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}