using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Mappers;
using Contracts.Responses;

namespace Application.Services;

public class AuditLogsService(IAuditLogRepository auditLogRepository, AuditLogMapper mapper) : IAuditLogsService
{
    public async Task<AuditLogResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id, cancellationToken);
        return auditLog == null
            ? throw new NotFoundException($"Audit Log with id: {id} not found")
            : mapper.AuditLogToResponse(auditLog);
    }

    public async Task<Slice<AuditLogResponse>> GetPagedAsync(int first = 10, string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default)
    {
        var pagedResult = await auditLogRepository.GetPagedAsync(first, after, before, cancellationToken);

        return new Slice<AuditLogResponse>
        {
            Items = mapper.AuditLogsToResponses(pagedResult.Items).ToList(),
            StartCursor = pagedResult.StartCursor,
            EndCursor = pagedResult.EndCursor,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage,
            TotalCount = pagedResult.TotalCount
        };
    }

    public async Task<AuditLogResponse> AppendAsync(AuditLogResponse auditLog,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditLog.NewStateJson) && string.IsNullOrWhiteSpace(auditLog.PreviousStateJson))
            throw new BadRequestException("Either state JSON is required");

        var entityToCreate = mapper.ResponseToAuditLog(auditLog);
        var created = await auditLogRepository.CreateAsync(entityToCreate, cancellationToken);
        return mapper.AuditLogToResponse(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditLog = await auditLogRepository.GetByIdAsync(id, cancellationToken);
        if (auditLog == null)
            throw new NotFoundException($"Audit Log with id: {id} not found");
        await auditLogRepository.DeleteAsync(auditLog.Id, cancellationToken);
    }
}