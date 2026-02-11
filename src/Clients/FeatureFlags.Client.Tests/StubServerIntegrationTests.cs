using System.Net;
using Contracts.Common;
using Contracts.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace FeatureFlags.Client.Tests;

public sealed class StubServerIntegrationTests
{
    [Fact]
    public async Task EvaluateAsync_WorksAgainstStubServer_EndToEnd()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.MapGet("/api/evaluation/{featureFlagKey}", (HttpRequest request, string featureFlagKey) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var auth) || auth != "Bearer ffsk_123")
                return Results.Unauthorized();

            return featureFlagKey == "always-on"
                ? Results.Ok(new { allowed = true, reason = "stubbed" })
                : Results.NotFound();
        });

        await app.StartAsync();

        var http = app.GetTestClient();
        var sdk = new FeatureFlagsClient(http, new FeatureFlagsClientOptions
        {
            BaseAddress = new Uri(http.BaseAddress!, "api/"),
            ApiKey = "ffsk_123"
        });

        var on = await sdk.EvaluateAsync("always-on", new EvaluationContext { UserId = "u1" });
        Assert.True(on.Allowed);

        var ex = await Assert.ThrowsAsync<FeatureFlagsApiException>(() => sdk.EvaluateAsync("missing-flag"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);

        await app.StopAsync();
    }
}