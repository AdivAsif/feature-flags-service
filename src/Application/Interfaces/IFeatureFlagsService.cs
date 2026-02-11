using Application.DTOs;

namespace Application.Interfaces;

public interface IFeatureFlagsService
{
    public Task<FeatureFlagDTO?> GetAsync(Guid projectId, Guid id);
    public Task<FeatureFlagDTO?> GetByKeyAsync(Guid projectId, string key);
    public Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(Guid projectId, int? take = null, int? skip = null);

    public Task<PagedDto<FeatureFlagDTO>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null);

    public Task<FeatureFlagDTO> CreateAsync(Guid projectId, FeatureFlagDTO featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null);

    public Task<FeatureFlagDTO> UpdateAsync(Guid projectId, string key, FeatureFlagDTO featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null);

    public Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null);
}