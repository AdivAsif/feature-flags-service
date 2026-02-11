using Application.DTOs;

namespace Application.Interfaces;

public interface IAuditLogsService
{
    Task<AuditLogDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedDto<AuditLogDto>> GetPagedAsync(int first = 10, string? after = null, string? before = null,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto> AppendAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}