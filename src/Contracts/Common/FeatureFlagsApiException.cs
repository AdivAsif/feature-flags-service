using System.Net;

namespace Contracts.Common;

public class FeatureFlagsApiException(
    HttpStatusCode statusCode,
    string message,
    string? responseBody = null,
    FeatureFlagsProblemDetails? problemDetails = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
    public FeatureFlagsProblemDetails? ProblemDetails { get; } = problemDetails;
}