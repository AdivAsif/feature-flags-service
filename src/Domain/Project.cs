using System.ComponentModel.DataAnnotations;

namespace Domain;

// Represents a project that can contain multiple feature flags. This allows for better organization and management of
// feature flags across different projects or applications. This is effectively the "tenant" in a multi-tenant architecture,
// as it provides a way to isolate feature flags and API keys. Meaning for different application, a new project will 
// have to be created.
public sealed class Project : EntityBase
{
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}