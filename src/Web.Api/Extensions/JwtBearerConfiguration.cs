using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Web.Api.Extensions;

public static class JwtBearerConfiguration
{
    public static AuthenticationBuilder AddJwtBearerAuthentication(this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        builder.AddJwtBearer(options =>
        {
            if (!string.IsNullOrWhiteSpace(configuration["JwtSecretKey"]))
            {
                options.Authority = configuration["Auth:Authority"];
                options.Audience = configuration["Auth:Audience"];
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSecretKey"])),
                    ValidateLifetime = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RoleClaimType = configuration["Auth:RoleClaimType"] ?? "role"
                };

                return;
            }

            options.Authority = configuration["Auth:Authority"];
            options.Audience = configuration["Auth:Audience"];

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                // Map the Auth0 role claim if you used a namespace
                RoleClaimType = configuration["Auth:RoleClaimType"] ?? "role"
            };

            // Configure SignalR authentication
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    // If the request is for our hub...
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/api/hubs/feature-flags"))
                        // Read the token out of the query string
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });

        return builder;
    }
}