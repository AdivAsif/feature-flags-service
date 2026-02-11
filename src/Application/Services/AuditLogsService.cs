using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public class AuditLogsService(IRepository<AuditLog> auditLogRepository, AuditLogMapper mapper) : IAuditLogsService
{
    public async Task<AuditLogDTO?> GetAsync(Guid id)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id);
        return auditLog == null
            ? throw new NotFoundException($"Audit Log with id: {id} not found")
            : mapper.AuditLogToAuditLogDto(auditLog);
    }

    public async Task<IEnumerable<AuditLogDTO>> GetAllAsync(int? take = null, int? skip = null)
    {
        return mapper.AuditLogsToAuditLogDtos(await auditLogRepository.GetAllAsync(take, skip));
    }

    public async Task<PagedDto<AuditLogDTO>> GetPagedAsync(int first = 10, string? after = null, string? before = null)
    {
        var pagedResult = await auditLogRepository.GetPagedAsync(first, after, before);

        return new PagedDto<AuditLogDTO>
        {
            Items = mapper.AuditLogsToAuditLogDtos(pagedResult.Items),
            PageInfo = new PaginationInfo
            {
                HasNextPage = pagedResult.PageInfo.HasNextPage,
                HasPreviousPage = pagedResult.PageInfo.HasPreviousPage,
                StartCursor = pagedResult.PageInfo.StartCursor,
                EndCursor = pagedResult.PageInfo.EndCursor,
                TotalCount = pagedResult.PageInfo.TotalCount
            }
        };
    }

    public async Task<AuditLogDTO> AppendAsync(AuditLogDTO auditLog)
    {
        if (string.IsNullOrWhiteSpace(auditLog.NewStateJson) && string.IsNullOrWhiteSpace(auditLog.PreviousStateJson))
            throw new BadRequestException("Either state JSON is required");

        var entityToCreate = mapper.AuditLogDtoToAuditLog(auditLog);
        var created = await auditLogRepository.CreateAsync(entityToCreate);
        return mapper.AuditLogToAuditLogDto(created);
    }

    public async Task DeleteAsync(Guid id)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id);
        if (auditLog == null)
            throw new NotFoundException($"Audit Log with id: {id} not found");
        await auditLogRepository.DeleteAsync(auditLog.Id);
    }
}