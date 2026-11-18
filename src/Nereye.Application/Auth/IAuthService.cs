namespace Nereye.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    /// <summary>Refresh token'ı iptal eder. <paramref name="userId"/> verildiyse token o kullanıcıya ait olmalı.</summary>
    Task LogoutAsync(Guid? userId, string refreshToken, CancellationToken cancellationToken = default);
    Task DeleteMeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uygulamasız hesap silme: kayıtlı bir e-postaysa tek kullanımlık onay bağlantısı gönderir.
    /// Kayıtlı olmayan adres için de aynı sessiz sonucu verir (hangi adresin kayıtlı olduğu sızmasın).
    /// <paramref name="link"/> (userId, token) → mutlak onay adresi.
    /// </summary>
    Task RequestDeletionAsync(string email, Func<Guid, string, string> link, CancellationToken cancellationToken = default);

    /// <summary>Bağlantıdaki kimlik ve token doğruysa hesabı siler; değilse false.</summary>
    Task<bool> ConfirmDeletionAsync(Guid userId, string token, CancellationToken cancellationToken = default);
    Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulama bağlantısı gönderir. E-posta sağlayıcısı yoksa <c>ServiceUnavailableException</c>.
    /// Adres zaten doğrulanmışsa bir şey göndermez.
    /// </summary>
    Task SendVerificationAsync(Guid userId, Func<Guid, string, string> link, CancellationToken cancellationToken = default);

    /// <summary>Bağlantıdaki token doğruysa e-postayı doğrulanmış işaretler.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    /// <summary>Yorum ve fotoğraf yazmak için e-posta doğrulaması şarttır.</summary>
    Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Şifre sıfırlama bağlantısı gönderir. Kayıtlı olmayan adres için de aynı sessiz sonuç.
    /// E-posta sağlayıcısı yoksa <c>ServiceUnavailableException</c> ("şifre sıfırlama şu an kapalı").
    /// </summary>
    Task RequestPasswordResetAsync(string email, Func<Guid, string, string> link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tek kullanımlık token ile yeni şifre koyar ve tüm oturumları kapatır. Token geçersiz, kullanılmış ya da
    /// süresi dolmuşsa false. Şifre kurala uymuyorsa <c>ValidationException</c>.
    /// </summary>
    Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Şifreyi değiştirir, diğer cihazlardaki oturumları kapatır ve yeni bir oturum döner.</summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
