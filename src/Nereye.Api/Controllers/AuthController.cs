using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Auth;
using Nereye.Application.Common;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthController> _log;

    public AuthController(IAuthService auth, IConfiguration config, IHostEnvironment env, ILogger<AuthController> log)
    {
        _auth = auth;
        _config = config;
        _env = env;
        _log = log;
    }

    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var session = await _auth.RegisterAsync(request, ct);
        // Doğrulama e-postası gönderilemezse kayıt yine başarılıdır; kullanıcı sonra yeniden isteyebilir.
        var baseUrl = PublicPages.BaseUrl(HttpContext, _config, _env);
        if (baseUrl is not null)
        {
            try
            {
                await _auth.SendVerificationAsync(session.UserId, (id, token) => EmailLinksController.VerifyLink(baseUrl, id, token), ct);
            }
            catch (Exception ex) when (ex is ServiceUnavailableException or HttpRequestException or InvalidOperationException)
            {
                _log.LogWarning(ex, "Doğrulama e-postası gönderilemedi");
            }
        }

        return Ok(session);
    }

    public sealed record VerificationResult(bool Sent, bool AlreadyVerified);

    /// <summary>
    /// Doğrulama bağlantısını yeniden gönderir. Adres zaten doğrulanmışsa mektup gitmez:
    /// <c>{ sent: false, alreadyVerified: true }</c>. Sağlayıcı ya da genel adres yoksa 503.
    /// </summary>
    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("verify-email")]
    public async Task<ActionResult<VerificationResult>> ResendVerification(CancellationToken ct)
    {
        if (await _auth.IsEmailVerifiedAsync(User.Id(), ct))
        {
            return Ok(new VerificationResult(false, true));
        }

        var baseUrl = PublicPages.BaseUrl(HttpContext, _config, _env)
                      ?? throw new ServiceUnavailableException("E-posta doğrulama şu an kapalı.");
        var sent = await _auth.SendVerificationAsync(User.Id(), (id, token) => EmailLinksController.VerifyLink(baseUrl, id, token), ct);
        return Ok(new VerificationResult(sent, !sent));
    }

    public sealed record ForgotRequest(string? Email);

    public const string ForgotAccepted = "Bu adres kayıtlıysa birkaç dakika içinde şifre sıfırlama bağlantısı gelecek.";

    /// <summary>
    /// Kayıtlı olsun olmasın aynı yanıt (hangi adresin kayıtlı olduğu sızmasın).
    /// Sağlayıcı ya da genel adres yoksa 503 "Şifre sıfırlama şu an kapalı."
    /// </summary>
    [EnableRateLimiting("auth")]
    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotRequest request, CancellationToken ct)
    {
        var baseUrl = PublicPages.BaseUrl(HttpContext, _config, _env)
                      ?? throw new ServiceUnavailableException("Şifre sıfırlama şu an kapalı.");
        await _auth.RequestPasswordResetAsync(request.Email ?? "", (id, token) => EmailLinksController.ResetLink(baseUrl, id, token), ct);
        return Accepted(new { message = ForgotAccepted });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    public sealed record RefreshRequest(string RefreshToken);

    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await _auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>
    /// Bu cihazın refresh token'ını iptal eder. Erişim token'ı gerekmez: süresi dolmuş olsa bile istemci,
    /// çıkış için aynı refresh token'la önce /refresh çağırmak zorunda kalmasın. Token'ı bilen onu iptal edebilir.
    /// </summary>
    [EnableRateLimiting("auth")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.Id() : null;
        await _auth.LogoutAsync(userId, request.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        Ok(await _auth.ChangePasswordAsync(User.Id(), request, ct));

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        await _auth.DeleteMeAsync(User.Id(), ct);
        return NoContent();
    }
}
