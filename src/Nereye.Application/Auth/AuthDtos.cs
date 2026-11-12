namespace Nereye.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, bool AcceptPrivacy = false);

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string? AvatarUrl);

public sealed record ProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    int ReviewCount,
    int FavoriteCount,
    int SavedPlaceCount);
