using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FeatureFlagsDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying migrations: {ex.Message}");
                throw;
            }
        }

        public async Task SeedDatabaseAsync(CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FeatureFlagsDbContext>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var seeder = new DbSeeder(dbContextFactory, configuration);

            try
            {
                await seeder.SeedAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding database: {ex.Message}");
                throw;
            }
        }
    }
}