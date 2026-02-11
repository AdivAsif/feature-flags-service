using Application.Interfaces;

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
                logger.LogInformation(
                    "Getting audit logs with cursor pagination (first: {First}, after: {After}, before: {Before})",
                    first, after ?? "null", before ?? "null");

                var pagedResult = await auditLogsService.GetPagedAsync(first, after, before);

                return Results.Ok(pagedResult);
            })
            .WithName("GetAllAuditLogs")
            .RequireAuthorization("ReadAccess");
    }
}