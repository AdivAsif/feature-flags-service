using System.Security.Claims;

namespace Web.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public Guid? GetProjectId()
        {
            var projectIdClaim = principal.FindFirst("projectId");
            if (projectIdClaim != null && Guid.TryParse(projectIdClaim.Value, out var projectId)) return projectId;
            return null;
        }
    }
}