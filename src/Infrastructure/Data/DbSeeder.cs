using Domain;
using Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

// Optional seeding of the database with default data for easier understanding of the project
public class DbSeeder(IDbContextFactory<FeatureFlagsDbContext> factory, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);

        var seedingEnabled = configuration.GetValue("DatabaseSeeding:Enabled", true);
        if (!seedingEnabled)
            return;

        // Check if any projects exist - if they do, don't seed to avoid conflicts with existing data
        if (await context.Projects.AnyAsync(cancellationToken))
            return;

        var createDefaultProject = configuration.GetValue("DatabaseSeeding:CreateDefaultProject", true);
        var createSampleApiKeys = configuration.GetValue("DatabaseSeeding:CreateSampleApiKeys", true);
        var createSampleFlags = configuration.GetValue("DatabaseSeeding:CreateSampleFlags", true);

        // If explicitly set to false, don't seed
        if (!createDefaultProject)
            return;

        // Create default project
        var defaultProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Default Project",
            Description = "Default project for getting started with feature flags and testing",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Projects.AddAsync(defaultProject, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Create default API keys for the project
        if (createSampleApiKeys)
        {
            var apiKeys = new[]
            {
                new
                {
                    Name = "Development SDK Key",
                    Scopes = "flags:read flags:write flags:delete",
                    Prefix = "ffsk_dev_"
                },
                new
                {
                    Name = "Production Read-Only Key",
                    Scopes = "flags:read",
                    Prefix = "ffsk_prod_"
                }
            };

            foreach (var keyConfig in apiKeys)
            {
                var apiKey = ApiKeyGenerator.GenerateKey(keyConfig.Prefix);
                var keyHash = ApiKeyHasher.HashKey(apiKey);
                var keyPrefix = ApiKeyGenerator.ExtractPrefix(apiKey);

                var apiKeyEntity = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    KeyHash = keyHash,
                    KeyPrefix = keyPrefix,
                    Name = keyConfig.Name,
                    Scopes = keyConfig.Scopes,
                    ExpiresAt = null,
                    CreatedByUserId = "seeder",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await context.ApiKeys.AddAsync(apiKeyEntity, cancellationToken);

                Console.WriteLine($"Created API key: {keyConfig.Name}");
                Console.WriteLine($"Key: {apiKey}");
                Console.WriteLine($"Scopes: {keyConfig.Scopes}");
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // Create sample feature flags
        if (createSampleFlags)
        {
            var sampleFlags = new[]
            {
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    Key = "new-dashboard",
                    Description = "Enable the new dashboard design for users",
                    Enabled = true,
                    Version = 1,
                    Parameters = [new FeatureFlagParameters { RuleType = 0, RuleValue = "50" }], // 50% rollout
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    Key = "dark-mode",
                    Description = "Enable dark mode UI theme",
                    Enabled = false,
                    Version = 1,
                    Parameters =
                    [
                        new FeatureFlagParameters { RuleType = RuleType.Group, RuleValue = "beta" }
                    ], // Enabled for users in the 'beta' group
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    Key = "beta-features",
                    Description = "Enable access to beta features",
                    Enabled = false,
                    Version = 1,
                    Parameters =
                    [
                        new FeatureFlagParameters { RuleType = RuleType.User, RuleValue = "user1" }
                    ], // Enabled for a specific user ID - "user1"
                    CreatedAt = DateTimeOffset.UtcNow
                }
            };

            await context.FeatureFlags.AddRangeAsync(sampleFlags, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        Console.WriteLine("Database seeding completed successfully.");

        if (createSampleApiKeys)
            Console.WriteLine("Copy the API keys above to test, they cannot be retrieved again in its full length.");
    }
}