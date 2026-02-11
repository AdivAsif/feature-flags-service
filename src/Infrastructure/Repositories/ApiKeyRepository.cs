using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly FeatureFlagsDbContext _context;

    public ApiKeyRepository(FeatureFlagsDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == apiKeyId, cancellationToken);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive && k.RevokedAt == null, cancellationToken);
    }

    public async Task<IEnumerable<ApiKey>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
        return apiKey;
    }

    public async Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        await _context.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.RevokedAt, DateTimeOffset.UtcNow)
                .SetProperty(k => k.IsActive, false)
                .SetProperty(k => k.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task UpdateLastUsedAtAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        await _context.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(k => k.LastUsedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }
}