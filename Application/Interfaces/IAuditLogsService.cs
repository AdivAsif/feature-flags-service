using Application.DTOs;
using Domain;

namespace Application.Interfaces;

public interface IAuditLogsService
{
    public Task<AuditLogDTO?> GetAsync(Guid id);

    public Task<IEnumerable<AuditLogDTO>> GetAllAsync(int? take = null, int? skip = null);
    public Task<PagedDto<AuditLogDTO>> GetPagedAsync(int first = 10, string? after = null, string? before = null);
    public Task<AuditLogDTO> AppendAsync(AuditLogDTO auditLog);
    // public Task<AuditLogDTO> CreateAsync(AuditLogDTO auditLog);
    // public Task<AuditLogDTO> UpdateAsync(Guid id, AuditLogDTO auditLog);
    public Task DeleteAsync(Guid id);
}