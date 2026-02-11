using System.Text.Json.Serialization;
using Application.DTOs;

namespace Web.Api.JsonContexts;

[JsonSerializable(typeof(EvaluationResultDto))]
internal partial class ApiJsonContext : JsonSerializerContext;