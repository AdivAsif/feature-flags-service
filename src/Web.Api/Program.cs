using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Mappers;
using Application.Services;
using Asp.Versioning;
using Domain;
using Infrastructure.Authentication;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using Web.Api.Extensions;
using Web.Api.Hubs;
using Web.Api.JsonContexts;
using Web.Api.Middleware;
using Web.Api.Services;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

// Clear default JWT claim type mappings to use short claim names
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddProblemDetails();

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

var logger = new LoggerConfiguration()
    .CreateLogger();

Log.Logger = logger;

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT"
        });

        document.Components.SecuritySchemes.Add("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key"
        });
        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer"),
                    ["api"]
                }
            }
        ];
        document.SetReferenceHostDocument();
        return Task.CompletedTask;
    });
});

builder.Services.AddPooledDbContextFactory<FeatureFlagsDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("FeatureFlagsDatabase") ??
                      throw new InvalidOperationException(
                          "Connection string 'FeatureFlagsDatabase' not found."),
        npgsql =>
        {
            npgsql.CommandTimeout(5);
            npgsql.MaxBatchSize(1);
        });

    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// Register base repositories
builder.Services.AddScoped<IFeatureFlagRepository, FeatureFlagsRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogsRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

// Register services BEFORE decorators
builder.Services.AddSingleton<ApiKeyUsageQueue>();
builder.Services.AddHostedService<ApiKeyUsageBackgroundService>();
builder.Services.AddHostedService<ApiKeyUsageCleanupService>();
builder.Services.AddSingleton<FeatureFlagMapper>();
builder.Services.AddSingleton<AuditLogMapper>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IFeatureFlagsService, FeatureFlagsService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();

// Register cached repositories and services as decorators
builder.Services.Decorate<IFeatureFlagRepository, CachedFeatureFlagRepository>();
builder.Services.Decorate<IProjectRepository, CachedProjectRepository>();
builder.Services.Decorate<IApiKeyRepository, CachedApiKeyRepository>();
builder.Services.Decorate<IEvaluationService, CachedEvaluationService>();

builder.Services.AddScoped<IAuditLogsService, AuditLogsService>();
builder.Services.AddSingleton<AuditLogQueue>();
builder.Services.AddHostedService<AuditLogBackgroundService>();
builder.Services.AddSingleton<CacheMetricsService>();
builder.Services.AddHostedService<CacheStatsService>();
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddMemoryCache(options =>
{
    // Configure L1 memory cache size for ~25,000 entries at peak
    // Assuming average entry size of ~2KB: 25,000 * 2KB = 50MB
    // Set to 100MB to provide headroom and prevent churning
    options.SizeLimit = 50000; // ~100MB with 2KB entries

    // Compact 25% when memory pressure detected
    options.CompactionPercentage = 0.25;
});

var redisCache = new RedisCache(new RedisCacheOptions
{
    Configuration = builder.Configuration.GetConnectionString("FeatureFlagsCache")
                    ?? throw new InvalidOperationException("Connection string 'FeatureFlagsCache' not found."),

    ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { builder.Configuration.GetConnectionString("FeatureFlagsCache")! },
        AbortOnConnectFail = false,
        ConnectTimeout = 1000,
        SyncTimeout = 1000,
        AsyncTimeout = 1000,
        ConnectRetry = 3,
        KeepAlive = 60
    }
});
var instrumentedRedisCache = new MetricsDistributedCache(redisCache);

builder.Services.AddFusionCache()
    .WithOptions(options =>
    {
        options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(2);
        options.FailSafeActivationLogLevel = LogLevel.Information;
        options.SerializationErrorsLogLevel = LogLevel.Warning;
        options.DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Debug;
        options.DistributedCacheErrorsLogLevel = LogLevel.Error;
        options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
        options.FactoryErrorsLogLevel = LogLevel.Error;

        options.EnableSyncEventHandlersExecution = true;
    })
    .WithDefaultEntryOptions(new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromMinutes(5),
        DistributedCacheDuration = TimeSpan.FromHours(1),

        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

        EagerRefreshThreshold = 0.8f,

        FactorySoftTimeout = TimeSpan.FromMilliseconds(10),
        FactoryHardTimeout = TimeSpan.FromMilliseconds(50),

        DistributedCacheSoftTimeout = TimeSpan.FromMilliseconds(100),
        DistributedCacheHardTimeout = TimeSpan.FromMilliseconds(500),

        AllowBackgroundDistributedCacheOperations = true,
        Size = 1,
        JitterMaxDuration = TimeSpan.FromMilliseconds(100)
    })
    .WithSerializer(
        new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions
        {
            TypeInfoResolver = ApiJsonContext.Default
        }))
    .WithDistributedCache(instrumentedRedisCache)
    .WithBackplane(
        new RedisBackplane(new RedisBackplaneOptions
        {
            Configuration = builder.Configuration.GetConnectionString("FeatureFlagsCache") ??
                            throw new InvalidOperationException(
                                "Connection string 'FeatureFlagsCache' not found.")
        })
    );
builder.Services.AddSignalR().AddStackExchangeRedis(builder.Configuration.GetConnectionString("FeatureFlagsCache") ??
                                                    throw new InvalidOperationException(
                                                        "Connection string 'FeatureFlagsCache' not found."));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearerAuthentication(builder.Configuration)
    .AddApiKeyAuthentication();

// Configure authorization policies
builder.Services.AddAuthorizationBuilder()
    // Configure authorization policies
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .Build())
    // Configure authorization policies
    .AddPolicy("Admin", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults
            .AuthenticationScheme);
        policy.RequireRole("admin");
    })
    // Configure authorization policies
    .AddPolicy("User", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults
            .AuthenticationScheme);
        policy.RequireRole("user", "admin");
    })
    // Configure authorization policies
    .AddPolicy("ReadAccess", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme);
        policy.RequireAssertion(context =>
        {
            // API Key with read scope
            var hasReadScope = context.User.HasClaim(c =>
                c.Type == "scope" && c.Value.Split(' ').Contains("flags:read"));

            // JWT with user or admin role
            var isUserOrAdmin = context.User.IsInRole("user") || context.User.IsInRole("admin");

            return hasReadScope || isUserOrAdmin;
        });
    })
    // Configure authorization policies
    .AddPolicy("WriteAccess", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme);
        policy.RequireAssertion(context =>
        {
            // API Key with write scope
            var hasWriteScope = context.User.HasClaim(c =>
                c.Type == "scope" && c.Value.Split(' ').Contains("flags:write"));

            // JWT with admin role
            var isAdmin = context.User.IsInRole("admin");

            return hasWriteScope || isAdmin;
        });
    })
    // Configure authorization policies
    .AddPolicy("DeleteAccess", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme);
        policy.RequireAssertion(context =>
        {
            // API Key with delete scope
            var hasDeleteScope = context.User.HasClaim(c =>
                c.Type == "scope" && c.Value.Split(' ').Contains("flags:delete"));

            // JWT with admin role
            var isAdmin = context.User.IsInRole("admin");

            return hasDeleteScope || isAdmin;
        });
    })
    // Configure authorization policies
    .AddPolicy("EvaluateAccess", policy =>
    {
        policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.DefaultScheme);
        policy.RequireAssertion(context =>
            context.User.HasClaim(c =>
                c.Type == "scope" && c.Value.Split(' ').Contains("flags:read")));
    });
builder.Services.AddHealthChecks();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddFusionCacheInstrumentation())
    .WithMetrics(metrics => metrics.AddFusionCacheInstrumentation());
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var app = builder.Build();

// Attach cache metrics monitoring
var cacheMetricsService = app.Services.GetRequiredService<CacheMetricsService>();
var fusionCache = app.Services.GetRequiredService<IFusionCache>();
cacheMetricsService.AttachToCache(fusionCache);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Environment.IsDevelopment() || (builder.Configuration.GetValue<bool>("EnableDevToken") &&
                                        !string.IsNullOrWhiteSpace(
                                            builder.Configuration.GetValue<string>("JwtSecretKey"))))
    app.MapPost("/dev/token", ([FromBody] DevTokenRequest devTokenRequest, ILogger<Program> loggerInput) =>
    {
        var roleClaimType = builder.Configuration["Auth:RoleClaimType"] ?? "role";
        var claims = new[]
        {
            new Claim("sub", devTokenRequest.UserId),
            new Claim("email", devTokenRequest.Email),
            new Claim("scope", string.Join(" ", devTokenRequest.Scopes)),
            new Claim(roleClaimType, devTokenRequest.Role)
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

app.MapHealthChecks("/health");
// Prometheus metrics endpoint - this is before the Authorization and Middleware registrations to make sure prometheus metrics are more accurate
app.MapMetrics();
app.UseHttpMetrics();

app.UseAuthentication();
app.UseHttpsRedirection();
app.UseAuthorization();
app.UseMiddleware<ETagMiddleware>();
app.MapHub<FeatureFlagHub>("/api/hubs/feature-flags").RequireAuthorization();

var api = app.NewVersionedApi();
var v1 = api.MapGroup("/api").HasApiVersion(new ApiVersion(1, 0));
v1.MapEndpoints(app.Services);

// Apply database migrations on startup
await app.Services.ApplyMigrationsAsync();

if (app.Environment.IsDevelopment())
    await app.Services.SeedDatabaseAsync();

app.Run();