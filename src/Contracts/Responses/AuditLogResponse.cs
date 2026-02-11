using Contracts.Common;
using Domain;

namespace Contracts.Responses;

public sealed class AuditLogResponse : ContractBase
{
    public Guid FeatureFlagId { get; init; }
    public AuditLogAction Action { get; init; }
    public string PerformedByUserId { get; init; } = string.Empty;
    public string PerformedByUserEmail { get; init; } = string.Empty;
    public string NewStateJson { get; init; } = string.Empty;
    public string PreviousStateJson { get; init; } = string.Empty;
}