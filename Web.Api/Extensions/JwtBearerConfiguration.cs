using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Web.Api.Extensions;

public static class JwtBearerConfiguration
{
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth:Authority"];
                options.Audience = configuration["Auth:Audience"];
                var secret = configuration["JwtSecretKey"] ?? configuration["Auth:Authority"] ??
                    throw new InvalidOperationException("JWT Secret Key not configured.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateLifetime = true,
                    ValidateIssuer = false,
                    ValidateAudience = false
                };

                // For minimum scope validation - disable for now
                // options.Events = new JwtBearerEvents
                // {
                //     OnTokenValidated = context =>
                //     {
                //         var claims = context.Principal?.Claims;
                //         var hasRequiredScope = claims?.Any(c => 
                //             c.Type == "scope" && 
                //             c.Value.Contains("flags:read")) ?? false;
                //         
                //         if (!hasRequiredScope)
                //         {
                //             context.Fail("Missing minimum required scope: flags:read");
                //         }
                //         
                //         return Task.CompletedTask;
                //     }
                // };
            });

        return services;
    }
}