using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

public class FeatureFlagsDbContextFactory : IDesignTimeDbContextFactory<FeatureFlagsDbContext>
{
    public FeatureFlagsDbContext CreateDbContext(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables();

        var config = builder.Build();
        var conn = config.GetConnectionString("FeatureFlagsDatabase")
                   ?? Environment.GetEnvironmentVariable("FEATUREFLAGS_CONNECTION")
                   ?? throw new InvalidOperationException("Connection string not found for design-time DbContext.");

        var options = new DbContextOptionsBuilder<FeatureFlagsDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new FeatureFlagsDbContext(options);
    }
}