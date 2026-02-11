namespace Web.Api.Extensions;

// Marker to disable ETag generation for an endpoint. This is useful for endpoints that return large payloads or when
// ETag generation is not needed, like Evaluations
public sealed class DisableETagMetadata;