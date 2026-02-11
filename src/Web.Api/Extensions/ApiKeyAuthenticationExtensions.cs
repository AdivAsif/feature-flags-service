using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace Web.Api.Extensions;

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            _ => { });

        return builder;
    }
}