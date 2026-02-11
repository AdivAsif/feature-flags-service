using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace Web.Api.Extensions;

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        // builder.Services.AddScoped<ApiKeyRepository>();

        builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            options => { });

        return builder;
    }
}