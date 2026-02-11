using System.Text.Json.Serialization;
using Contracts.Common;
using Contracts.Responses;

namespace Web.Api.JsonContexts;

[JsonSerializable(typeof(EvaluationResponse))]
[JsonSerializable(typeof(FeatureFlagResponse))]
[JsonSerializable(typeof(PagedResult<FeatureFlagResponse>))]
[JsonSerializable(typeof(AuditLogResponse))]
[JsonSerializable(typeof(PagedResult<AuditLogResponse>))]
[JsonSerializable(typeof(PageInfo))]
internal partial class ApiJsonContext : JsonSerializerContext;