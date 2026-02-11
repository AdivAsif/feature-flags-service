using Contracts.Common;

namespace Contracts.Responses;

public sealed class ProjectResponse : ContractBase
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}