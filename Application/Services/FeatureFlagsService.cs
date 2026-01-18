using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public sealed class FeatureFlagsService(IRepository<FeatureFlag> featureFlagRepository, FeatureFlagMapper mapper)
    : IFeatureFlagsService
{
    public async Task<FeatureFlagDTO?> GetAsync(Guid id)
    {
        var featureFlag = await featureFlagRepository.GetByIdAsync(id);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with id: {id} not found");
        return mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<FeatureFlagDTO?> GetByKeyAsync(string key)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found");
        return mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(int? take = null, int? skip = null)
    {
        return mapper.FeatureFlagsToFeatureFlagDtos(await featureFlagRepository.GetAllAsync(take, skip));
    }

    public async Task<FeatureFlagDTO> CreateAsync(FeatureFlagDTO featureFlag)
    {
        if (featureFlag.Key == null) throw new BadRequestException("Feature Flag key is required");
        if (await featureFlagRepository.GetByKeyAsync(featureFlag.Key) != null)
            throw new BadRequestException($"Feature Flag with key: {featureFlag.Key} already exists");

        var entityToCreate = mapper.FeatureFlagDtoToFeatureFlag(featureFlag);
        var created = await featureFlagRepository.CreateAsync(entityToCreate);

        return mapper.FeatureFlagToFeatureFlagDto(created);
    }

    public async Task<FeatureFlagDTO> UpdateAsync(string key, FeatureFlagDTO featureFlag)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BadRequestException("Feature Flag key is required");

        var featureFlagFromDb = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlagFromDb == null)
            throw new NotFoundException($"Feature Flag with key: {featureFlag.Key} not found");

        // Disallow changing the canonical key via update
        if (!string.IsNullOrEmpty(featureFlag.Key) && !string.Equals(featureFlag.Key, key, StringComparison.Ordinal))
            throw new BadRequestException(
                "Changing a Feature Flag key is not allowed. Create a new flag or use an explicit rename operation.");

        featureFlagFromDb.Description = featureFlag.Description;
        featureFlagFromDb.Enabled = featureFlag.Enabled;
        featureFlagFromDb.Parameters = featureFlag.Parameters;
        featureFlagFromDb.Version++;
        featureFlagFromDb.UpdatedAt = DateTime.UtcNow;

        var updated = await featureFlagRepository.UpdateAsync(featureFlagFromDb);

        return mapper.FeatureFlagToFeatureFlagDto(updated);
    }

    public async Task DeleteByKeyAsync(string key)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found");
        await featureFlagRepository.DeleteAsync(featureFlag.Id);
    }
}