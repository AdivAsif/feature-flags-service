using System.Security.Cryptography;
using System.Text;

namespace Web.Api.Middleware;

public class ETagMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status200OK &&
            context.Request.Method == HttpMethods.Get)
        {
            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            var etag = GenerateETag(responseBody);

            context.Response.Headers.ETag = etag;

            if (context.Request.Headers.TryGetValue("If-None-Match", out var incomingEtag) &&
                incomingEtag == etag)
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                await originalBodyStream.FlushAsync();
                return;
            }
        }

        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalBodyStream);
    }

    private static string GenerateETag(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"\"{Convert.ToBase64String(hash)}\"";
    }
}