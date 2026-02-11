using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("✓ Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error applying migrations: {ex.Message}");
            throw;
        }
    }

    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var seeder = new DbSeeder(dbContext, configuration);

        try
        {
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error seeding database: {ex.Message}");
            throw;
        }
    }
}