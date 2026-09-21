namespace Nereye.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, bool AcceptPrivacy = false);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string? AvatarUrl);
