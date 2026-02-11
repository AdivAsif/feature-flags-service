namespace SharedKernel;

/// <summary>
///     Marker interface for entities that support multi-tenancy.
///     Used to generate project-scoped cache keys.
/// </summary>
public interface IMultiTenant
{
    Guid ProjectId { get; }
}