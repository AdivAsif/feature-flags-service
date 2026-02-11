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
    public static string HeaderName => "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISystemClock clock,
    IApiKeyRepository apiKeyRepository,
    ApiKeyUsageQueue apiKeyUsageQueue,
    IFusionCache cache)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder, clock)
{
    private static readonly FusionCacheEntryOptions AuthCacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(1),
        Size = 1,
        SkipDistributedCacheRead = true,
        SkipDistributedCacheWrite = true
    };

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try custom header first
        if (Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeaderValues))
            return await ValidateApiKeyAsync(apiKeyHeaderValues.ToString());

        // Try Authorization header second
        if (Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            var authHeader = authHeaderValues.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var potentialApiKey = authHeader.Substring("Bearer ".Length).Trim();
                if (potentialApiKey.StartsWith("ffsk_", StringComparison.OrdinalIgnoreCase))
                    return await ValidateApiKeyAsync(potentialApiKey);
            }
        }

        // Try query string (common for SignalR/WebSockets)
        // Optimization: Only check for query string on hub/websocket endpoints
        if (Request.Path.StartsWithSegments("/api/hubs"))
        {
            if (Request.Query.TryGetValue("access_token", out var queryValues))
            {
                var potentialKey = queryValues.ToString();
                if (potentialKey.StartsWith("ffsk_", StringComparison.OrdinalIgnoreCase))
                    return await ValidateApiKeyAsync(potentialKey);
            }
        }
        
        return AuthenticateResult.NoResult();
    }

    private async Task<AuthenticateResult> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            // First check if we have a cached result for this raw API key (fast path)
            var authCacheKey = $"auth:apikey:{apiKey}";

            var maybeTicket =
                await cache.TryGetAsync<AuthenticationTicket>(authCacheKey, AuthCacheOptions, Context.RequestAborted);
            if (maybeTicket.HasValue)
            {
                var cachedTicket = maybeTicket.Value!;
                // Background update usage
                if (!cachedTicket.Principal.HasClaim(c => c.Type == "apiKeyId"))
                    return AuthenticateResult.Success(cachedTicket);
                var idClaim = cachedTicket.Principal.FindFirst("apiKeyId")?.Value;
                if (Guid.TryParse(idClaim, out var apiKeyId))
                    apiKeyUsageQueue.TryQueue(apiKeyId);

                return AuthenticateResult.Success(cachedTicket);
            }

            var keyHash = ApiKeyHasher.HashKey(apiKey);
            var apiKeyEntity = await apiKeyRepository.GetByKeyHashAsync(keyHash, Context.RequestAborted);

            if (apiKeyEntity == null)
            {
                // We return NoResult here instead of Fail to allow other authentication handlers 
                // (like JwtBearer) to attempt authentication if this wasn't actually a valid API key.
                // Logger.LogWarning("API key not found in database: {ApiKeyPrefix}...",
                //     apiKey[..Math.Min(apiKey.Length, 10)]);
                return AuthenticateResult.NoResult();
            }

            // Check expiration
            if (apiKeyEntity.ExpiresAt.HasValue && apiKeyEntity.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                Logger.LogError("Expired API key attempted: {ApiKeyId}", apiKeyEntity.Id);
                return AuthenticateResult.Fail("API key has expired");
            }

            // Queue last-used update (handled by a hosted service with its own DI scope/DbContext).
            apiKeyUsageQueue.TryQueue(apiKeyEntity.Id);

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
            await cache.SetAsync(authCacheKey, ticket, AuthCacheOptions, Context.RequestAborted);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating API key");
            return AuthenticateResult.Fail("Error validating API key");
        }
    }
}