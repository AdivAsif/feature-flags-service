using Domain;
using Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

public class DbSeeder(IDbContextFactory<FeatureFlagsDbContext> factory, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);

        var seedingEnabled = configuration.GetValue("DatabaseSeeding:Enabled", true);
        if (!seedingEnabled)
        {
            Console.WriteLine("✓ Database seeding is disabled in configuration");
            return;
        }

        // Check if any projects exist
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            Console.WriteLine("✓ Database already seeded, skipping...");
            return;
        }

        Console.WriteLine("→ Seeding database with default data...");

        var createDefaultProject = configuration.GetValue("DatabaseSeeding:CreateDefaultProject", true);
        var createSampleApiKeys = configuration.GetValue("DatabaseSeeding:CreateSampleApiKeys", true);
        var createSampleFlags = configuration.GetValue("DatabaseSeeding:CreateSampleFlags", true);

        if (!createDefaultProject)
        {
            Console.WriteLine("✓ Default project creation disabled in configuration");
            return;
        }

        // Create default project
        var defaultProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Default Project",
            Description = "Default project for getting started with feature flags",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await context.Projects.AddAsync(defaultProject, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"  ✓ Created default project: {defaultProject.Name} (ID: {defaultProject.Id})");

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
                    CreatedByUserId = "system",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                await context.ApiKeys.AddAsync(apiKeyEntity, cancellationToken);

                Console.WriteLine($"  ✓ Created API key: {keyConfig.Name}");
                Console.WriteLine($"    Key: {apiKey}");
                Console.WriteLine($"    Scopes: {keyConfig.Scopes}");
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
                    Key = "welcome-banner",
                    Description = "Display welcome banner for new users",
                    Enabled = true,
                    Version = 1,
                    Parameters = [new FeatureFlagParameters { RuleType = 0, RuleValue = "50" }],
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    Key = "dark-mode",
                    Description = "Enable dark mode UI theme",
                    Enabled = false,
                    Version = 1,
                    Parameters = [new FeatureFlagParameters { RuleType = RuleType.Group, RuleValue = "beta" }],
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProject.Id,
                    Key = "beta-features",
                    Description = "Enable access to beta features",
                    Enabled = false,
                    Version = 1,
                    Parameters = [new FeatureFlagParameters { RuleType = RuleType.User, RuleValue = "user1" }],
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            await context.FeatureFlags.AddRangeAsync(sampleFlags, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            Console.WriteLine($"  ✓ Created {sampleFlags.Length} sample feature flags");
        }

        Console.WriteLine("✓ Database seeding completed successfully!");

        if (createSampleApiKeys)
        {
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("IMPORTANT: Save the API keys above - they won't be shown again!");
            Console.WriteLine("=".PadRight(80, '='));
        }
    }
}