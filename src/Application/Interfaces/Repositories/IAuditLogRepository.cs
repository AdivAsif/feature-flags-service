using Application.Common;
using Domain;

namespace Application.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Slice<AuditLog>> GetPagedAsync(int first = 10, string? after = null, string? before = null,
        CancellationToken cancellationToken = default);

    Task<AuditLog> CreateAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}