using Contracts.Responses;
using Domain;
using Riok.Mapperly.Abstractions;

namespace Application.Mappers;

[Mapper]
public partial class AuditLogMapper
{
    public partial AuditLogResponse AuditLogToResponse(AuditLog entity);
    public partial IEnumerable<AuditLogResponse> AuditLogsToResponses(IEnumerable<AuditLog> entities);

    public partial AuditLog ResponseToAuditLog(AuditLogResponse response);
}