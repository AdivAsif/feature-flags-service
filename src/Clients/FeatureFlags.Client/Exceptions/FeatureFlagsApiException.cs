using System.Net;
using FeatureFlags.Client.ProblemDetails;

namespace FeatureFlags.Client.Exceptions;

public class FeatureFlagsApiException : Exception
{
    public FeatureFlagsApiException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        FeatureFlagsProblemDetails? problemDetails = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ProblemDetails = problemDetails;
    }

    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }
    public FeatureFlagsProblemDetails? ProblemDetails { get; }
}