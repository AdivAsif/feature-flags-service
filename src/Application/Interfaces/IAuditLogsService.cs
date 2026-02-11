using Application.Common;
using Contracts.Responses;

namespace Application.Interfaces;

public interface IAuditLogsService
{
    Task<AuditLogResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Slice<AuditLogResponse>> GetPagedAsync(int first = 10, string? after = null, string? before = null,
        CancellationToken cancellationToken = default);

    Task<AuditLogResponse> AppendAsync(AuditLogResponse auditLog, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}