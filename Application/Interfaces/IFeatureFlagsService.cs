using Application.DTOs;

namespace Application.Interfaces;

public interface IFeatureFlagsService
{
    public Task<FeatureFlagDTO?> GetAsync(Guid id);
    public Task<FeatureFlagDTO?> GetByKeyAsync(string key);
    public Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(int? take = null, int? skip = null);
    public Task<PagedFeatureFlagDTO> GetPagedAsync(int first = 10, string? after = null, string? before = null);
    public Task<FeatureFlagDTO> CreateAsync(FeatureFlagDTO featureFlag);
    public Task<FeatureFlagDTO> UpdateAsync(string key, FeatureFlagDTO featureFlag);
    public Task DeleteByKeyAsync(string key);
}