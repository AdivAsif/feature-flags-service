namespace Domain;

public sealed record DevTokenRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public string Role { get; set; } = string.Empty;
}