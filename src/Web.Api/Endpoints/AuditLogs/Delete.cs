using Application.Exceptions;
using Application.Interfaces;

namespace Web.Api.Endpoints.AuditLogs;

public class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/audit-logs/{id:guid}",
            async (Guid id, IAuditLogsService auditLogsService, ILogger<Delete> logger) =>
            {
                try
                {
                    await auditLogsService.DeleteAsync(id);
                    return Results.NoContent();
                }
                catch (NotFoundException ex)
                {
                    logger.LogError(ex, "Audit log by id does not exist: {ID}", id);
                    return Results.NotFound(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while deleting audit log by id: {ID}", id);
                    return Results.Problem(statusCode: 500, detail: "An unexpected error occurred.");
                }
            }).RequireAuthorization("DeleteAccess");
    }
}