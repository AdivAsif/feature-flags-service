namespace Domain;

// Only for development purposes, not for production use. This allows developers to generate tokens without needing to
// go through the API key management process. These tokens should have a short lifespan and limited permissions to
// minimize security risks if they are accidentally exposed.
public sealed record DevTokenRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = []; // Not important
    public string Role { get; set; } = string.Empty; // Has to be admin
}