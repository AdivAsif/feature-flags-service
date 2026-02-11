using System.Security.Claims;

namespace Web.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetProjectId(this ClaimsPrincipal principal)
    {
        var projectIdClaim = principal.FindFirst("projectId");
        if (projectIdClaim != null && Guid.TryParse(projectIdClaim.Value, out var projectId)) return projectId;
        return null;
    }

    public static Guid? GetApiKeyId(this ClaimsPrincipal principal)
    {
        var apiKeyIdClaim = principal.FindFirst("apiKeyId");
        if (apiKeyIdClaim != null && Guid.TryParse(apiKeyIdClaim.Value, out var apiKeyId)) return apiKeyId;
        return null;
    }

    public static bool IsApiKeyAuthentication(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(c => c.Type == "apiKeyId");
    }

    public static bool IsJwtAuthentication(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(c => c.Type == "role");
    }
}