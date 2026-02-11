using Domain;
using Riok.Mapperly.Abstractions;
using SharedKernel;

namespace Application.DTOs;

public class AuditLogDTO : DtoBase
{
    public Guid FeatureFlagId { get; set; } // foreign key to feature flag affected
    public AuditLogAction Action { get; set; } = AuditLogAction.Create;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string PerformedByUserEmail { get; set; } = string.Empty;
    public string NewStateJson { get; set; } = string.Empty; // new state of feature flag
    public string PreviousStateJson { get; set; } = string.Empty; // previous state of feature flag, null if created
}

[Mapper]
public partial class AuditLogMapper
{
    // Get
    public partial AuditLogDTO AuditLogToAuditLogDto(AuditLog auditLog);
    public partial IEnumerable<AuditLogDTO> AuditLogsToAuditLogDtos(IEnumerable<AuditLog> auditLogs);

    // Create
    public partial AuditLog AuditLogDtoToAuditLog(AuditLogDTO dto);

    // Update
    public partial void AuditLogDtoToAuditLog(AuditLogDTO dto, [MappingTarget] AuditLog entity);
}