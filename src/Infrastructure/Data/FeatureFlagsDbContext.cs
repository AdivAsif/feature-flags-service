using System.Text.Json;
using System.Text.Json.Serialization;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data;

public class FeatureFlagsDbContext(DbContextOptions<FeatureFlagsDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Enabled).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.Parameters).HasConversion(p =>
                        JsonSerializer.Serialize(p, JsonOptions),
                    p => JsonSerializer.Deserialize<FeatureFlagParameters[]>(p, JsonOptions) ??
                         Array.Empty<FeatureFlagParameters>())
                .Metadata.SetValueComparer(
                    new ValueComparer<FeatureFlagParameters[]>(
                        (a, b) =>
                            (a == null && b == null) ||
                            (a != null && b != null && ((IEnumerable<FeatureFlagParameters>)a).SequenceEqual(b)),
                        v =>
                            v.Aggregate(0,
                                (hash, item) =>
                                    HashCode.Combine(hash, item.RuleType, item.RuleValue)),
                        v => v.ToArray()
                    )
                );
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => new { e.ProjectId, e.Key }).IsUnique();
            entity.HasIndex(e => new { e.ProjectId, e.Key })
                .IncludeProperties(e => new { e.Enabled, e.Version, e.Parameters, e.Description });
            entity.HasIndex(e => new { e.ProjectId, e.Key, e.Enabled, e.Version });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FeatureFlagId).IsRequired();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50)
                .HasConversion(new EnumToStringConverter<AuditLogAction>());
            entity.Property(e => e.PerformedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PerformedByUserEmail).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.IsActive, e.Name });
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Scopes).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasIndex(e => e.KeyHash)
                .IsUnique()
                .IncludeProperties(e => new { e.ProjectId, e.IsActive, e.Scopes });
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => new { e.ProjectId, e.IsActive });
        });
    }
}