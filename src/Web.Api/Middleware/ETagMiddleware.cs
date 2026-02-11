using System.Security.Cryptography;
using Web.Api.Extensions;

namespace Web.Api.Middleware;

public class ETagMiddleware(RequestDelegate next)
{
    private const long MaxBodyBytesToHash = 64 * 1024;
    private static readonly PathString EvaluationPathPrefix = new("/api/evaluation");
    private static readonly PathString EvaluationV1PathPrefix = new("/api/v1/evaluation");

    public async Task InvokeAsync(HttpContext context)
    {
        // Avoid buffering/hashing for the hot-path evaluation endpoint
        // Evaluation responses are user-context dependent and don't need ETags, and buffering would add unnecessary overhead
        if (context.Request.Path.StartsWithSegments(EvaluationPathPrefix) || 
            context.Request.Path.StartsWithSegments(EvaluationV1PathPrefix))
        {
            await next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<DisableETagMetadata>() != null)
        {
            await next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status200OK &&
            context.Request.Method == HttpMethods.Get)
            if (memoryStream.Length <= MaxBodyBytesToHash)
            {
                memoryStream.Position = 0;
                var etag = GenerateETag(memoryStream);

                context.Response.Headers.ETag = etag;

                if (context.Request.Headers.TryGetValue("If-None-Match", out var incomingEtag) &&
                    incomingEtag == etag)
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = 0;
                    await originalBodyStream.FlushAsync(context.RequestAborted);
                    return;
                }
            }

        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
    }

    private static string GenerateETag(Stream contentStream)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(contentStream);
        return $"\"{Convert.ToBase64String(hash)}\"";
    }
}