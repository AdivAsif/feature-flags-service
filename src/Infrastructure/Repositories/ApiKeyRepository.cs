using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ApiKeyRepository(IDbContextFactory<FeatureFlagsDbContext> contextFactory)
    : BaseRepository<FeatureFlagsDbContext>(contextFactory), IApiKeyRepository
{
    // GET
    public Task<ApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(db => db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == apiKeyId, cancellationToken), cancellationToken);
    }

    public Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db => await db.ApiKeys
                .AsNoTracking()
                .Select(k => new ApiKey
                {
                    Id = k.Id,
                    ProjectId = k.ProjectId,
                    IsActive = k.IsActive,
                    KeyHash = k.KeyHash,
                    RevokedAt = k.RevokedAt,
                    Scopes = k.Scopes,
                    ExpiresAt = k.ExpiresAt,
                    Name = k.Name
                })
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive && k.RevokedAt == null, cancellationToken),
            cancellationToken);
    }

    public async Task<IEnumerable<ApiKey>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db => await db.ApiKeys
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken), cancellationToken);
    }

    // CREATE
    public Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            await db.ApiKeys.AddAsync(apiKey, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return apiKey;
        }, cancellationToken);
    }

    // UPDATE
    public Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default) // (soft-delete)
    {
        return ExecuteAsync(async db => await db.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.RevokedAt, DateTimeOffset.UtcNow)
                .SetProperty(k => k.IsActive, false)
                .SetProperty(k => k.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken), cancellationToken);
    }

    public Task UpdateLastUsedAtAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async db => await db.ApiKeys
                .Where(k => k.Id == apiKeyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(k => k.LastUsedAt, DateTimeOffset.UtcNow),
                    cancellationToken), cancellationToken);
    }
}