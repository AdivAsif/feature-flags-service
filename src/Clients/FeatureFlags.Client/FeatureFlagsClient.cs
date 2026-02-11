using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Contracts.Common;
using Contracts.Models;
using Contracts.Requests;
using Contracts.Responses;
using Microsoft.AspNetCore.SignalR.Client;

namespace FeatureFlags.Client;

public sealed class FeatureFlagsClient : IFeatureFlagsClient, IFeatureFlagsManagementClient, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry<EvaluationResponse>> _evaluationCache =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, CacheEntry<FeatureFlagResponse>> _featureFlagCacheByKey =
        new(StringComparer.Ordinal);

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FeatureFlagsClientOptions _options;
    private HubConnection? _hubConnection;

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

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    // This returns whether a flag is allowed or not, with a string reason as well
    public async Task<EvaluationResponse> EvaluateAsync(
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
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Feature flag not found.",
                "Unauthorized.",
                "Feature flag evaluation failed.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<EvaluationResponse>(_jsonOptions, cancellationToken);
        var effectiveResult = result ?? new EvaluationResponse
            { Allowed = false, Reason = "Empty response body." }; // Default result, to not break any applications

        if (_options.EnableEvaluationCache)
            SetCache(_evaluationCache, cacheKey, effectiveResult, _options.EvaluationCacheDuration);

        return effectiveResult;
    }

    // This returns whether a flag is allowed or not, just the bool - most likely what most consumers will want to call
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
            return defaultValue; // User gets to set the default value (false by default)
        }
    }

    // SignalR support - real-time functionality
    public event Action<string>? OnFlagChanged;

    public async Task StartListeningAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
            return;

        var hubUrl = new Uri(_options.BaseAddress!, "hubs/feature-flags");
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(_options.ApiKey))
                    // For WebSockets, we pass the API key as the access token in the query string
                    // as most browsers/proxies don't support custom headers on the WebSocket handshake.
                    options.AccessTokenProvider = () => Task.FromResult(_options.ApiKey)!;
                else if (!string.IsNullOrEmpty(_options.BearerToken))
                    options.AccessTokenProvider = () => Task.FromResult(_options.BearerToken)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("FlagChanged", key =>
        {
            // Invalidate internal caches
            _evaluationCache.TryRemove(key, out _);
            _featureFlagCacheByKey.TryRemove(key, out _);

            // Notify subscribers
            OnFlagChanged?.Invoke(key);
        });

        _hubConnection.On<string>("SubscriptionFailed", reason =>
        {
            // Log or handle subscription failure
            Console.WriteLine($"[FeatureFlags SDK] Subscription failed: {reason}");
        });

        await _hubConnection.StartAsync(cancellationToken);

        // Even if we auto-subscribe on server, calling it explicitly doesn't hurt 
        // and ensures we use the provided projectId if available.
        await _hubConnection.InvokeAsync("SubscribeToProject", projectId, cancellationToken);
    }

    // Projects - this won't work without a JWT bearer token with an admin role
    public async Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WithApiVersion("projects"));
        ApplyHeaders(request, null);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Projects not found.",
                "Unauthorized.",
                "Failed to fetch projects.",
                cancellationToken);
        var result =
            await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(_jsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, WithApiVersion("projects"));
        httpRequest.Content = JsonContent.Create(request);
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Project not found.",
                "Unauthorized.",
                "Failed to create project.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ProjectResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");
    }

    public async Task<ProjectResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, WithApiVersion($"projects/{projectId:D}"));
        httpRequest.Content = JsonContent.Create(request);
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Project not found.",
                "Unauthorized.",
                "Failed to update project.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ProjectResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, WithApiVersion($"projects/{projectId:D}"));
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        throw await CreateApiExceptionAsync(
            response,
            "Project not found.",
            "Unauthorized.",
            "Failed to delete project.",
            cancellationToken);
    }

    // API Keys
    public async Task<IReadOnlyList<ApiKeyResponse>> GetApiKeysByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WithApiVersion($"projects/{projectId:D}/apikeys"));
        ApplyHeaders(request, null);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Project not found.",
                "Unauthorized.",
                "Failed to fetch API keys.",
                cancellationToken);
        var result =
            await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>(_jsonOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<ApiKeyResponse> CreateApiKeyAsync(Guid projectId, CreateApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest =
            new HttpRequestMessage(HttpMethod.Post, WithApiVersion($"projects/{projectId:D}/apikeys"));
        httpRequest.Content = JsonContent.Create(request);
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Project not found.",
                "Unauthorized.",
                "Failed to create API key.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiKeyResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");
    }

    public async Task RevokeApiKeyAsync(Guid projectId, Guid keyId, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete,
            WithApiVersion($"projects/{projectId:D}/apikeys/{keyId:D}"));
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        throw await CreateApiExceptionAsync(
            response,
            "Project or API key not found.",
            "Unauthorized.",
            "Failed to revoke API key.",
            cancellationToken);
    }

    public async Task<PagedResult<FeatureFlagResponse>> GetFeatureFlagsAsync(
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
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Feature flags not found.",
                "Unauthorized.",
                "Failed to fetch feature flags.",
                cancellationToken);
        var result =
            await response.Content.ReadFromJsonAsync<PagedResult<FeatureFlagResponse>>(_jsonOptions,
                cancellationToken);
        return result ?? new PagedResult<FeatureFlagResponse>();
    }

    public async Task<FeatureFlagResponse> GetFeatureFlagByKeyAsync(string key,
        CancellationToken cancellationToken = default)
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

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Feature flag not found.",
                "Unauthorized.",
                "Failed to fetch feature flag.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<FeatureFlagResponse>(_jsonOptions, cancellationToken);
        if (result is null)
            throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");

        if (_options.EnableEtagCaching && response.Headers.ETag is not null)
            SetCache(_featureFlagCacheByKey, cacheKey, result, TimeSpan.FromMinutes(10),
                response.Headers.ETag.ToString());

        return result;
    }

    public async Task<FeatureFlagResponse> CreateFeatureFlagAsync(CreateFeatureFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, WithApiVersion("feature-flags"));
        httpRequest.Content = JsonContent.Create(request);
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Project not found.",
                "Unauthorized.",
                "Failed to create feature flag.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<FeatureFlagResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");
    }

    public async Task<FeatureFlagResponse> UpdateFeatureFlagAsync(string key, UpdateFeatureFlagRequest request,
        string? ifMatch = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Feature flag key is required.", nameof(key));
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch,
            WithApiVersion($"feature-flags/{Uri.EscapeDataString(key)}"));
        httpRequest.Content = JsonContent.Create(request);
        ApplyHeaders(httpRequest, null);

        if (!string.IsNullOrWhiteSpace(ifMatch))
            httpRequest.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Feature flag not found.",
                "Unauthorized.",
                "Failed to update feature flag.",
                cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<FeatureFlagResponse>(_jsonOptions, cancellationToken);
        if (result is null)
            throw new FeatureFlagsApiException(response.StatusCode, "Empty response body.");

        var cacheKey = BuildFeatureFlagCacheKey(key);
        _featureFlagCacheByKey.TryRemove(cacheKey, out _);
        return result;
    }

    public async Task DeleteFeatureFlagAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Feature flag key is required.", nameof(key));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete,
            WithApiVersion($"feature-flags/{Uri.EscapeDataString(key)}"));
        ApplyHeaders(httpRequest, null);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(
                response,
                "Feature flag not found.",
                "Unauthorized.",
                "Failed to delete feature flag.",
                cancellationToken);
        var cacheKey = BuildFeatureFlagCacheKey(key);
        _featureFlagCacheByKey.TryRemove(cacheKey, out _);
    }

    public Task<EvaluationResponse> EvaluateAsync(
        string featureFlagKey,
        string userId,
        string? email = null,
        IEnumerable<string>? groups = null,
        // string? tenantId = null,
        // string? environment = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
    {
        return EvaluateAsync(
            featureFlagKey,
            new EvaluationContext
            {
                UserId = userId,
                Groups = groups?.ToList()
                // TenantId = tenantId,
                // Environment = environment
            },
            requestOptions,
            cancellationToken);
    }

    public Task<bool> IsEnabledAsync(
        string featureFlagKey,
        string userId,
        string? email = null,
        IEnumerable<string>? groups = null,
        // string? tenantId = null,
        // string? environment = null,
        FeatureFlagsRequestOptions? requestOptions = null,
        bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        return IsEnabledAsync(
            featureFlagKey,
            new EvaluationContext
            {
                UserId = userId,
                Groups = groups?.ToList()
                // TenantId = tenantId,
                // Environment = environment
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

        // if (!string.IsNullOrWhiteSpace(effectiveContext.Email))
        //     query.Add(new KeyValuePair<string, string>("email", effectiveContext.Email));

        if (effectiveContext.Groups is { } groupsList && groupsList.Any())
            query.Add(new KeyValuePair<string, string>("groups", string.Join(",", groupsList)));

        // if (!string.IsNullOrWhiteSpace(effectiveContext.TenantId))
        //     query.Add(new KeyValuePair<string, string>("tenantId", effectiveContext.TenantId));
        //
        // if (!string.IsNullOrWhiteSpace(effectiveContext.Environment))
        //     query.Add(new KeyValuePair<string, string>("environment", effectiveContext.Environment));

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

        // Reason to put it in the Bearer header is that for SignalR/WebSocket connections, the Bearer token is more commonly used and recognized by the server.
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

    /*
     * TODO: Environment and ProjectId
            $"v={version}|k={featureFlagKey}|u={ctx.UserId}|e={ctx.Email}|g={groups}|t={ctx.ProjectId}|env={ctx.Environment}";
     * As is it implemented now, projects are the primary unit of isolation, meaning additional API keys and projects per
     * service, in the future Tenants will be the primary unit of isolation, owning multiple projects - meaning that
     * the service will acquire the tenant ID from the API key in the future, and flags will have a project ID parameter,
     * same with |e={ctx.Email}
     */
    private string BuildEvaluationCacheKey(string featureFlagKey, EvaluationContext? context)
    {
        var ctx = context ?? new EvaluationContext();
        var groups = ctx.Groups is { } groupsList && groupsList.Any() ? string.Join(",", groupsList) : string.Empty;
        var version = _options.ApiVersion?.ToString() ?? string.Empty;
        return
            $"v={version}|k={featureFlagKey}|u={ctx.UserId}|g={groups}";
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