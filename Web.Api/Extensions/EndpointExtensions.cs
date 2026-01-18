using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using System.Security.Claims;
using Domain;

namespace Web.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(Endpoints.IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(Endpoints.IEndpoint), type));

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<Endpoints.IEndpoint>>();

        foreach (var endpoint in endpoints)
            endpoint.MapEndpoint(app);

        return app;
    }

    public static EvaluationContext ToEvaluationContext(this ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var groups = user.FindAll("group").Select(c => c.Value).ToList();
        var tenantId = user.FindFirstValue("tenant_id");
        var environment = user.FindFirstValue("environment");

        return new EvaluationContext(userId, groups, tenantId, environment);
    }
}
