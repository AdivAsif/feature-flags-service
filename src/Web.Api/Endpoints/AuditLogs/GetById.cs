using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.AuditLogs;

public class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/audit-logs/{id:guid}",
                async (Guid id, IAuditLogsService auditLogsService,
                    ILogger<GetById> logger) =>
                {
                    try
                    {
                        logger.LogDebug("Getting audit log by id: {ID}", id);
                        var auditLog = await auditLogsService.GetAsync(id);

                        return auditLog == null
                            ? Results.NotFound($"Audit log with id: {id} not found")
                            : Results.Ok(auditLog);
                    }
                    catch (NotFoundException ex)
                    {
                        logger.LogError(ex, "Audit log with id: {ID} not found", id);
                        return Results.NotFound(ex.Message);
                    }
                    catch (BadRequestException ex)
                    {
                        logger.LogError(ex, "An error occurred while getting audit log by id: {ID}", id);
                        return Results.BadRequest(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred while getting audit log by id: {ID}", id);
                        return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                    }
                })
            .WithName("GetAuditLogById")
            .RequireAuthorization("ReadAccess");
    }
}