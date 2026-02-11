namespace Contracts.Requests;

public sealed record CreateApiKeyRequest(string Name, string Scopes, DateTimeOffset? ExpiresAt = null);