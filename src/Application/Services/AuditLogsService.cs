using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public class AuditLogsService(IRepository<AuditLog> auditLogRepository, AuditLogMapper mapper) : IAuditLogsService
{
    public async Task<AuditLogDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id, cancellationToken);
        return auditLog == null
            ? throw new NotFoundException($"Audit Log with id: {id} not found")
            : mapper.AuditLogToAuditLogDto(auditLog);
    }

    public async Task<PagedDto<AuditLogDto>> GetPagedAsync(int first = 10, string? after = null, string? before = null,
        CancellationToken cancellationToken = default)
    {
        var pagedResult = await auditLogRepository.GetPagedAsync(first, after, before, cancellationToken);

        return new PagedDto<AuditLogDto>
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

    public async Task<AuditLogDto> AppendAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditLog.NewStateJson) && string.IsNullOrWhiteSpace(auditLog.PreviousStateJson))
            throw new BadRequestException("Either state JSON is required");

        var entityToCreate = mapper.AuditLogDtoToAuditLog(auditLog);
        var created = await auditLogRepository.CreateAsync(entityToCreate, cancellationToken);
        return mapper.AuditLogToAuditLogDto(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id, cancellationToken);
        if (auditLog == null)
            throw new NotFoundException($"Audit Log with id: {id} not found");
        await auditLogRepository.DeleteAsync(auditLog.Id, cancellationToken);
    }
}