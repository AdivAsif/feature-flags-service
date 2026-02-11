using Application.DTOs;

namespace Application.Interfaces;

public interface IFeatureFlagsService
{
    public Task<FeatureFlagDTO?> GetAsync(Guid id);
    public Task<FeatureFlagDTO?> GetByKeyAsync(string key);
    public Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(int? take = null, int? skip = null);
    public Task<PagedDto<FeatureFlagDTO>> GetPagedAsync(int first = 10, string? after = null, string? before = null);

    public Task<FeatureFlagDTO> CreateAsync(FeatureFlagDTO featureFlag, string? performedByUserId = null,
        string? performedByUserEmail = null);

    public Task<FeatureFlagDTO> UpdateAsync(string key, FeatureFlagDTO featureFlag, string? performedByUserId = null,
        string? performedByUserEmail = null);

    public Task DeleteByKeyAsync(string key, string? performedByUserId = null, string? performedByUserEmail = null);
}