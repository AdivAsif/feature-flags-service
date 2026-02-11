using System.Text.Json;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data;

public class FeatureFlagsDbContext(DbContextOptions<FeatureFlagsDbContext> options) : DbContext(options)
{
    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Enabled).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.Parameters).HasConversion(p =>
                    JsonSerializer.Serialize(p, (JsonSerializerOptions?)null),
                p => JsonSerializer.Deserialize<FeatureFlagParameters[]>(p, (JsonSerializerOptions?)null) ??
                     Array.Empty<FeatureFlagParameters>());
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => new { e.Key, e.Enabled, e.Version });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FeatureFlagId).IsRequired();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50).HasConversion(new EnumToStringConverter<AuditLogAction>());
            entity.Property(e => e.PerformedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PerformedByUserEmail).IsRequired().HasMaxLength(100);
            // entity.Property(e => e.NewFeatureFlagState).HasConversion(s =>
            //         JsonSerializer.Serialize(s, (JsonSerializerOptions?)null),
            //     s => JsonSerializer.Deserialize<FeatureFlag>(s, (JsonSerializerOptions?)null) ??
            //          new FeatureFlag()).IsRequired();
            // entity.Property(e => e.PreviousFeatureFlagState).IsRequired(false).HasConversion(s =>
            //         JsonSerializer.Serialize(s, (JsonSerializerOptions?)null),
            //     s => JsonSerializer.Deserialize<FeatureFlag>(s, (JsonSerializerOptions?)null) ??
            //          null);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}