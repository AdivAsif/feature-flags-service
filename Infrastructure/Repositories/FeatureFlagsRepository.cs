using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Repositories;

public sealed class FeatureFlagsRepository(FeatureFlagsDbContext dbContext) : IRepository<FeatureFlag>
{
    // GET
    public async Task<FeatureFlag?> GetByIdAsync(Guid id)
    {
        return await dbContext.FeatureFlags.FindAsync(id);
    }

    public async Task<FeatureFlag?> GetByKeyAsync(string key)
    {
        return await dbContext.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(ff => ff.Key == key);
    }

    public async Task<IEnumerable<FeatureFlag>> GetAllAsync(int? take, int? skip)
    {
        return await dbContext.FeatureFlags.AsNoTracking().ToListAsync();
        // TODO: add pagination
    }

    // CREATE
    public async Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag)
    {
        await dbContext.FeatureFlags.AddAsync(featureFlag);
        await dbContext.SaveChangesAsync();
        return featureFlag;
    }

    // UPDATE
    public async Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag)
    {
        dbContext.Entry(featureFlag).State = EntityState.Modified;
        // dbContext.FeatureFlags.Update(featureFlag);
        await dbContext.SaveChangesAsync();
        return featureFlag;
    }

    // DELETE
    public async Task DeleteAsync(Guid id)
    {
        var featureFlag = await dbContext.FeatureFlags.FindAsync(id);
        if (featureFlag == null) return;
        dbContext.FeatureFlags.Remove(featureFlag);
        await dbContext.SaveChangesAsync();
    }
}