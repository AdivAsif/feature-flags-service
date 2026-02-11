using System.ComponentModel.DataAnnotations;

namespace Domain;

public abstract class EntityBase
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}