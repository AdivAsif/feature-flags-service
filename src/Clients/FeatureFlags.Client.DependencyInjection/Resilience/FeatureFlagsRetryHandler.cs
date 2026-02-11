using System.Net;
using Microsoft.Extensions.Logging;

namespace FeatureFlags.Client.DependencyInjection.Resilience;

internal sealed class FeatureFlagsRetryHandler(
    FeatureFlagsClientOptions options,
    ILogger<FeatureFlagsRetryHandler>? logger = null)
    : DelegatingHandler
{
    private readonly Random _random = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!options.EnableRetries || options.MaxRetries <= 0 ||
            (options.RetryOnlyIdempotentRequests && !IsIdempotent(request.Method)))
            return await base.SendAsync(request, cancellationToken);

        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            Exception? exception = null;

            try
            {
                response = await base.SendAsync(request, cancellationToken);
                if (!IsRetryable(response.StatusCode))
                    return response;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                exception = ex;
            }

            if (attempt == options.MaxRetries) return exception is not null ? throw exception : response!;

            response?.Dispose();

            var delay = ComputeDelay(attempt);
            logger?.LogWarning(
                "Retrying feature flags HTTP request (attempt {Attempt}/{MaxRetries}) after {DelayMs}ms",
                attempt + 1,
                options.MaxRetries,
                (int)delay.TotalMilliseconds);

            await Task.Delay(delay, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or TaskCanceledException;
    }

    private static bool IsIdempotent(HttpMethod method)
    {
        return method == HttpMethod.Get ||
               method == HttpMethod.Head ||
               method == HttpMethod.Options ||
               method == HttpMethod.Delete ||
               method == HttpMethod.Put;
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            return true;

        return code is >= 500 and <= 599;
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var baseMs = options.RetryBaseDelay.TotalMilliseconds;
        var maxMs = options.RetryMaxDelay.TotalMilliseconds;

        var exponential = baseMs * Math.Pow(2, attempt);
        var clamped = Math.Min(exponential, maxMs);

        var jitter = _random.NextDouble() * (clamped * 0.2);
        return TimeSpan.FromMilliseconds(clamped + jitter);
    }
}