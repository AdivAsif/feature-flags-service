using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FeatureFlags.Client.Exceptions;
using FeatureFlags.Client.ProblemDetails;

namespace FeatureFlags.Client;

public sealed class FeatureFlagsClient : IFeatureFlagsClient, IFeatureFlagsManagementClient
{
    private readonly ConcurrentDictionary<string, CacheEntry<EvaluationResult>> _evaluationCache =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, CacheEntry<FeatureFlag>> _featureFlagCacheByKey =
        new(StringComparer.Ordinal);

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FeatureFlagsClientOptions _options;

    public FeatureFlagsClient(HttpClient httpClient, FeatureFlagsClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        if (options.BaseAddress is null)
            throw new ArgumentException("BaseAddress is required.", nameof(options));

        _httpClient.BaseAddress = options.BaseAddress;
        if (options.Timeout is not null)
            _httpClient.Timeout = options.Timeout.Value;

        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<EvaluationResult> EvaluateAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureFlagKey))
            throw new ArgumentException("Feature flag key is required.", nameof(featureFlagKey));

        var cacheKey = BuildEvaluationCacheKey(featureFlagKey, context);
        if (_options.EnableEvaluationCache && TryGetFromCache(_evaluationCache, cacheKey, out var cached))
            return cached.Value;

        var requestUri = BuildEvaluationUri(featureFlagKey, context);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        ApplyHeaders(request, requestOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<EvaluationResult>(_jsonOptions, cancellationToken);
            var effectiveResult = result ?? new EvaluationResult { Allowed = false, Reason = "Empty response body." };

            if (_options.EnableEvaluationCache)
                SetCache(_evaluationCache, cacheKey, effectiveResult, _options.EvaluationCacheDuration);

            return effectiveResult;
        }

        throw await CreateApiExceptionAsync(
            response,
            "Feature flag not found.",
            "Unauthorized.",
            "Feature flag evaluation failed.",
            cancellationToken);
    }

    public async Task<bool> IsEnabledAsync(
        string featureFlagKey,
        EvaluationContext? context = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await EvaluateAsync(featureFlagKey, context, requestOptions, cancellationToken);
            return result.Allowed;
        }
        catch (FeatureFlagsApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return defaultValue;
        }
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WithApiVersion("projects"));
        ApplyHeaders(request, null);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<List<Project>>(_jsonOptions, cancellationToken);
            return result ?? [];
        }

        throw await CreateApiExceptionAsync(
            response,
            "Projects not found.",
            "Unauthorized.",
            "Failed to fetch projects.",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WithApiVersion($"projects/{projectId:D}/apikeys"));
        ApplyHeaders(request, null);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<List<ApiKey>>(_jsonOptions, cancellationToken);
            return result ?? [];
        }

        throw await CreateApiExceptionAsync(
            response,
            "Project not found.",
            "Unauthorized.",
            "Failed to fetch API keys.",
            cancellationToken);
    }

    public async Task<PagedResult<FeatureFlag>> GetFeatureFlagsAsync(
        int first = 10,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("first", first.ToString())
        };

        if (!string.IsNullOrWhiteSpace(after))
            query.Add(new KeyValuePair<string, string>("after", after));
        if (!string.IsNullOrWhiteSpace(before))
            query.Add(new KeyValuePair<string, string>("before", before));
        if (_options.ApiVersion is not null)
            query.Add(new KeyValuePair<string, string>("api-version", _options.ApiVersion.ToString()));

        var uri = query.Count == 0 ? "feature-flags" : $"feature-flags?{ToQueryString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyHeaders(request, null);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result =
                await response.Content.ReadFromJsonAsync<PagedResult<FeatureFlag>>(_jsonOptions, cancellationToken);
            return result ?? new PagedResult<FeatureFlag>();
        }

        throw await CreateApiExceptionAsync(
            response,
            "Feature flags not found.",
            "Unauthorized.",
            "Failed to fetch feature flags.",
            cancellationToken);
    }

    public async Task<FeatureFlag> GetFeatureFlagByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Feature flag key is required.", nameof(key));

        var cacheKey = BuildFeatureFlagCacheKey(key);
        string? ifNoneMatch = null;
        if (_options.EnableEtagCaching && TryGetFromCache(_featureFlagCacheByKey, cacheKey, out var cached))
            ifNoneMatch = cached.ETag;

        using var request =
            new HttpRequestMessage(HttpMethod.Get, WithApiVersion($"feature-flags/{Uri.EscapeDataString(key)}"));
        ApplyHeaders(request, null);

        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified && _options.EnableEtagCaching)
            if (TryGetFromCache(_featureFlagCacheByKey, cacheKey, out var cachedAgain))
                return cachedAgain.Value;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<FeatureFlag>(_jsonOptions, cancellationToken);
            if (result is null)
                throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");

            if (_options.EnableEtagCaching && response.Headers.ETag is not null)
                SetCache(_featureFlagCacheByKey, cacheKey, result, TimeSpan.FromMinutes(10),
                    response.Headers.ETag.ToString());

            return result;
        }

        throw await CreateApiExceptionAsync(
            response,
            "Feature flag not found.",
            "Unauthorized.",
            "Failed to fetch feature flag.",
            cancellationToken);
    }

    public Task<EvaluationResult> EvaluateAsync(
        string featureFlagKey,
        string userId,
        string? email = null,
        IEnumerable<string>? groups = null,
        string? tenantId = null,
        string? environment = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
    {
        return EvaluateAsync(
            featureFlagKey,
            new EvaluationContext
            {
                UserId = userId,
                Email = email,
                Groups = groups?.ToArray(),
                TenantId = tenantId,
                Environment = environment
            },
            requestOptions,
            cancellationToken);
    }

    public Task<bool> IsEnabledAsync(
        string featureFlagKey,
        string userId,
        string? email = null,
        IEnumerable<string>? groups = null,
        string? tenantId = null,
        string? environment = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        return IsEnabledAsync(
            featureFlagKey,
            new EvaluationContext
            {
                UserId = userId,
                Email = email,
                Groups = groups?.ToArray(),
                TenantId = tenantId,
                Environment = environment
            },
            requestOptions,
            defaultValue,
            cancellationToken);
    }

    private string BuildEvaluationUri(string featureFlagKey, EvaluationContext? context)
    {
        var query = new List<KeyValuePair<string, string>>();

        if (_options.ApiVersion is not null)
            query.Add(new KeyValuePair<string, string>("api-version", _options.ApiVersion.ToString()));

        var effectiveContext = context ?? new EvaluationContext();

        if (!string.IsNullOrWhiteSpace(effectiveContext.UserId))
            query.Add(new KeyValuePair<string, string>("userId", effectiveContext.UserId));

        if (!string.IsNullOrWhiteSpace(effectiveContext.Email))
            query.Add(new KeyValuePair<string, string>("email", effectiveContext.Email));

        if (effectiveContext.Groups is { Count: > 0 })
            query.Add(new KeyValuePair<string, string>("groups", string.Join(",", effectiveContext.Groups)));

        if (!string.IsNullOrWhiteSpace(effectiveContext.TenantId))
            query.Add(new KeyValuePair<string, string>("tenantId", effectiveContext.TenantId));

        if (!string.IsNullOrWhiteSpace(effectiveContext.Environment))
            query.Add(new KeyValuePair<string, string>("environment", effectiveContext.Environment));

        var basePath = $"evaluation/{Uri.EscapeDataString(featureFlagKey)}";
        return query.Count == 0 ? basePath : $"{basePath}?{ToQueryString(query)}";
    }

    private static string ToQueryString(IEnumerable<KeyValuePair<string, string>> query)
    {
        return string.Join("&",
            query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private void ApplyHeaders(HttpRequestMessage request, FeatureFlagsRequestOptions? requestOptions)
    {
        ApplyAuthentication(request);

        foreach (var kvp in _options.DefaultHeaders)
        {
            request.Headers.Remove(kvp.Key);
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        }
        else if (request.Headers.UserAgent.Count == 0)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
            request.Headers.UserAgent.ParseAdd($"FeatureFlags.Client/{version}");
        }

        if (requestOptions is null)
            return;

        foreach (var kvp in requestOptions.Headers)
        {
            request.Headers.Remove(kvp.Key);
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        request.Headers.Remove(_options.ApiKeyHeaderName);
        request.Headers.Authorization = null;

        var bearer = _options.BearerToken?.Trim();
        if (!string.IsNullOrWhiteSpace(bearer))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return;
        }

        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Either ApiKey or BearerToken must be provided.");

        if (apiKey.StartsWith("ffsk_", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return;
        }

        request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, apiKey);
    }

    private async Task<FeatureFlagsApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        string notFoundMessage,
        string unauthorizedMessage,
        string defaultMessage,
        CancellationToken cancellationToken)
    {
        var body = await SafeReadBodyAsync(response, cancellationToken);
        var problem = TryParseProblemDetails(response, body);

        var message = response.StatusCode switch
        {
            HttpStatusCode.NotFound => notFoundMessage,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => unauthorizedMessage,
            _ => defaultMessage
        };

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
            message = problem.Detail!;

        return new FeatureFlagsApiException(response.StatusCode, message, body, problem);
    }

    private FeatureFlagsProblemDetails? TryParseProblemDetails(HttpResponseMessage response, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return JsonSerializer.Deserialize<FeatureFlagsProblemDetails>(body, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetFromCache<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key,
        out CacheEntry<T> entry)
    {
        if (cache.TryGetValue(key, out entry!))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return true;

            cache.TryRemove(key, out _);
        }

        entry = default!;
        return false;
    }

    private static void SetCache<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, T value,
        TimeSpan duration, string? etag = null)
    {
        cache[key] = new CacheEntry<T>(value, DateTimeOffset.UtcNow.Add(duration), etag);
    }

    private string BuildEvaluationCacheKey(string featureFlagKey, EvaluationContext? context)
    {
        var ctx = context ?? new EvaluationContext();
        var groups = ctx.Groups is { Count: > 0 } ? string.Join(",", ctx.Groups) : string.Empty;
        var version = _options.ApiVersion?.ToString() ?? string.Empty;
        return
            $"v={version}|k={featureFlagKey}|u={ctx.UserId}|e={ctx.Email}|g={groups}|t={ctx.TenantId}|env={ctx.Environment}";
    }

    private string BuildFeatureFlagCacheKey(string key)
    {
        var version = _options.ApiVersion?.ToString() ?? string.Empty;
        return $"v={version}|k={key}";
    }

    private string WithApiVersion(string path)
    {
        if (_options.ApiVersion is null)
            return path;

        var separator = path.Contains('?') ? "&" : "?";
        return $"{path}{separator}api-version={Uri.EscapeDataString(_options.ApiVersion.ToString())}";
    }

    private readonly record struct CacheEntry<T>(T Value, DateTimeOffset ExpiresAt, string? ETag);
}