using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Interfaces.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private static readonly FusionCacheEntryOptions AuthCacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(1),
        Size = 1,
        SkipDistributedCacheRead = true,
        SkipDistributedCacheWrite = true
    };

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ApiKeyUsageQueue _apiKeyUsageQueue;
    private readonly IFusionCache _cache;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IApiKeyRepository apiKeyRepository,
        ApiKeyUsageQueue apiKeyUsageQueue,
        IFusionCache cache)
        : base(options, logger, encoder, clock)
    {
        _apiKeyRepository = apiKeyRepository;
        _apiKeyUsageQueue = apiKeyUsageQueue;
        _cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try to get API key from custom header first
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
        {
            // Try Authorization header with Bearer scheme
            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
                return AuthenticateResult.NoResult();

            var authHeader = authHeaderValues.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            var potentialApiKey = authHeader.Substring("Bearer ".Length).Trim();

            // Check if it looks like an API key (starts with our prefix)
            if (!potentialApiKey.StartsWith("ffsk_", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            return await ValidateApiKeyAsync(potentialApiKey);
        }

        var apiKey = apiKeyHeaderValues.ToString();
        return await ValidateApiKeyAsync(apiKey);
    }

    private async Task<AuthenticateResult> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            // First check if we have a cached result for this raw API key (fast path)
            var authCacheKey = $"auth:apikey:{apiKey}";

            var maybeTicket =
                await _cache.TryGetAsync<AuthenticationTicket>(authCacheKey, AuthCacheOptions, Context.RequestAborted);
            if (maybeTicket.HasValue)
            {
                var cachedTicket = maybeTicket.Value!;
                // Background update usage
                if (cachedTicket.Principal.HasClaim(c => c.Type == "apiKeyId"))
                {
                    var idClaim = cachedTicket.Principal.FindFirst("apiKeyId")?.Value;
                    if (Guid.TryParse(idClaim, out var apiKeyId))
                        _ = _apiKeyUsageQueue.QueueApiKeyUsageAsync(apiKeyId, Context.RequestAborted);
                }

                return AuthenticateResult.Success(cachedTicket);
            }

            var keyHash = ApiKeyHasher.HashKey(apiKey);
            var apiKeyEntity = await _apiKeyRepository.GetByKeyHashAsync(keyHash, Context.RequestAborted);

            if (apiKeyEntity == null)
            {
                Logger.LogError("Invalid API key attempted");
                return AuthenticateResult.Fail("Invalid API key");
            }

            // Check expiration
            if (apiKeyEntity.ExpiresAt.HasValue && apiKeyEntity.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                Logger.LogError("Expired API key attempted: {ApiKeyId}", apiKeyEntity.Id);
                return AuthenticateResult.Fail("API key has expired");
            }

            // Queue last-used update (handled by a hosted service with its own DI scope/DbContext).
            _ = _apiKeyUsageQueue.QueueApiKeyUsageAsync(apiKeyEntity.Id, Context.RequestAborted);

            // Build claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, apiKeyEntity.Id.ToString()),
                new("projectId", apiKeyEntity.ProjectId.ToString()),
                new("apiKeyId", apiKeyEntity.Id.ToString()),
                new("apiKeyName", apiKeyEntity.Name)
            };

            // Add scopes as individual claims
            var scopes = apiKeyEntity.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var scope in scopes) claims.Add(new Claim("scope", scope));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // Logger.LogDebug("API key authenticated successfully: {ApiKeyId} for project {ProjectId}",
            //     apiKeyEntity.Id, apiKeyEntity.ProjectId);

            // Cache the successful authentication ticket for ~1 minute to avoid SHA256 and repository lookup
            // Note: We only use L1 (memory) cache here because AuthenticationTicket is not easily serializable for Redis.
            await _cache.SetAsync(authCacheKey, ticket, AuthCacheOptions, Context.RequestAborted);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating API key");
            return AuthenticateResult.Fail("Error validating API key");
        }
    }
}