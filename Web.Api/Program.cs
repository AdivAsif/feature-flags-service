using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using SharedKernel;
using Web.Api.Extensions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
var logger = new LoggerConfiguration()
    .CreateLogger();

Log.Logger = logger;

builder.Services.AddOpenApi();
builder.Services.AddNpgsql<FeatureFlagsDbContext>(builder.Configuration.GetConnectionString("FeatureFlagsDatabase") ??
                                                  throw new InvalidOperationException(
                                                      "Connection string 'FeatureFlagsDatabase' not found."));
builder.Services.AddScoped<IRepository<FeatureFlag>, FeatureFlagsRepository>();
builder.Services.AddSingleton<FeatureFlagMapper>();
builder.Services.AddScoped<IFeatureFlagsService, FeatureFlagsService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.Decorate(typeof(IRepository<>), typeof(CachedRepository<>));
builder.Services.AddMemoryCache();
builder.Services.AddFusionCache()
    .WithOptions(options =>
    {
        options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(2);

        options.FailSafeActivationLogLevel = LogLevel.Debug;
        options.SerializationErrorsLogLevel = LogLevel.Warning;
        options.DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Debug;
        options.DistributedCacheErrorsLogLevel = LogLevel.Error;
        options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
        options.FactoryErrorsLogLevel = LogLevel.Error;
    })
    .WithDefaultEntryOptions(new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromMinutes(1),

        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

        EagerRefreshThreshold = 0.9f,

        FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
        FactoryHardTimeout = TimeSpan.FromMilliseconds(1500),

        DistributedCacheSoftTimeout = TimeSpan.FromSeconds(1),
        DistributedCacheHardTimeout = TimeSpan.FromSeconds(2),
        AllowBackgroundDistributedCacheOperations = true,

        JitterMaxDuration = TimeSpan.FromSeconds(2)
    })
    .WithSerializer(
        new FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(
        new RedisCache(new RedisCacheOptions
        {
            Configuration = builder.Configuration.GetConnectionString("FeatureFlagsCache") ??
                            throw new InvalidOperationException(
                                "Connection string 'FeatureFlagsCache' not found.")
        })
    )
    .WithBackplane(
        new RedisBackplane(new RedisBackplaneOptions
        {
            Configuration = builder.Configuration.GetConnectionString("FeatureFlagsCache") ??
                            throw new InvalidOperationException(
                                "Connection string 'FeatureFlagsCache' not found.")
        })
    );

builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanReadFlags", policy => policy.RequireClaim("scope", "flags:read"));
    options.AddPolicy("CanWriteFlags", policy => policy.RequireClaim("scope", "flags:write"));
    options.AddPolicy("CanDeleteFlags", policy => policy.RequireClaim("scope", "flags:delete"));

    options.AddPolicy("User", policy => policy.RequireClaim("role", "user"));
    options.AddPolicy("Admin", policy => policy.RequireClaim("role", "admin"));

    options.AddPolicy("ReadAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "scope" && c.Value.Split(' ').Contains("flags:read"))
            || context.User.HasClaim("role", "user")
            || context.User.HasClaim("role", "admin")));

    options.AddPolicy("WriteAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "scope" && c.Value.Split(' ').Contains("flags:write"))
            || context.User.HasClaim("role", "admin")));

    options.AddPolicy("DeleteAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "admin") ||
            context.User.HasClaim(c => c.Type == "scope" && c.Value.Split(' ').Contains("flags:delete"))));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapPost("/dev/token", ([FromBody] DevTokenRequest devTokenRequest, ILogger<Program> loggerInput) =>
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, devTokenRequest.UserId),
            new Claim(ClaimTypes.Email, devTokenRequest.Email),
            new Claim("scope", string.Join(" ", devTokenRequest.Scopes)),
            new Claim("role", devTokenRequest.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration.GetSection("JwtSecretKey").Value ??
            throw new InvalidOperationException(
                "JWT Secret Key not configured.")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            builder.Configuration.GetSection("JwtIssuer").Value,
            builder.Configuration.GetSection("JwtAudience").Value,
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials
        );

        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), expires = token.ValidTo });
    }).AllowAnonymous();
}

app.UseAuthentication();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapEndpoints();

app.Run();