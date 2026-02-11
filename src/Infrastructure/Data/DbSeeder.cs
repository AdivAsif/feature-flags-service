using Domain;
using Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

public class DbSeeder
{
    private readonly IConfiguration _configuration;
    private readonly FeatureFlagsDbContext _context;

    public DbSeeder(FeatureFlagsDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        var seedingEnabled = _configuration.GetValue("DatabaseSeeding:Enabled", true);
        if (!seedingEnabled)
        {
            Console.WriteLine("✓ Database seeding is disabled in configuration");
            return;
        }

        // Check if any projects exist
        if (await _context.Projects.AnyAsync())
        {
            Console.WriteLine("✓ Database already seeded, skipping...");
            return;
        }

        Console.WriteLine("→ Seeding database with default data...");

        var createDefaultProject = _configuration.GetValue("DatabaseSeeding:CreateDefaultProject", true);
        var createSampleApiKeys = _configuration.GetValue("DatabaseSeeding:CreateSampleApiKeys", true);
        var createSampleFlags = _configuration.GetValue("DatabaseSeeding:CreateSampleFlags", true);

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

        await _context.Projects.AddAsync(defaultProject);
        await _context.SaveChangesAsync();

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

                await _context.ApiKeys.AddAsync(apiKeyEntity);

                Console.WriteLine($"  ✓ Created API key: {keyConfig.Name}");
                Console.WriteLine($"    Key: {apiKey}");
                Console.WriteLine($"    Scopes: {keyConfig.Scopes}");
            }

            await _context.SaveChangesAsync();
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
                    Parameters = Array.Empty<FeatureFlagParameters>(),
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
                    Parameters = Array.Empty<FeatureFlagParameters>(),
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
                    Parameters = Array.Empty<FeatureFlagParameters>(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            await _context.FeatureFlags.AddRangeAsync(sampleFlags);
            await _context.SaveChangesAsync();

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