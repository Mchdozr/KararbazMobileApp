namespace Nereye.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
    Task DeleteMeAsync(Guid userId, CancellationToken cancellationToken = default);
}
