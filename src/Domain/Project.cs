using System.ComponentModel.DataAnnotations;
using SharedKernel;

namespace Domain;

public sealed class Project : EntityBase
{
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}