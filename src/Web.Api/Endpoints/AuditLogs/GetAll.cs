using Application.Interfaces;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.AuditLogs;

public class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/audit-logs", async (
                IAuditLogsService auditLogsService,
                ILogger<GetAll> logger,
                int first = 10,
                string? after = null,
                string? before = null) =>
            {
                var slice = await auditLogsService.GetPagedAsync(first, after, before);
                return Results.Ok(slice.ToPagedResult());
            })
            .WithName("GetAllAuditLogs")
            .RequireAuthorization("ReadAccess");
    }
}