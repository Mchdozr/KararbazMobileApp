using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Auth;
using Nereye.Application.Common;

namespace Nereye.Api.Controllers;

/// <summary>E-postadaki bağlantıların açıldığı sayfalar: e-posta doğrulama ve şifre sıfırlama.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting("auth")]
public sealed class EmailLinksController : ControllerBase
{
    private const string ResetTitle = "Yeni şifre belirle";

    private readonly IAuthService _auth;

    public EmailLinksController(IAuthService auth) => _auth = auth;

    /// <summary>Bağlantılar bu yollarla kurulur; kök <c>Api:PublicBaseUrl</c>'den gelir.</summary>
    public static string VerifyLink(string baseUrl, Guid userId, string token) =>
        $"{baseUrl}/eposta-dogrula?u={userId:N}&t={Uri.EscapeDataString(token)}";

    public static string ResetLink(string baseUrl, Guid userId, string token) =>
        $"{baseUrl}/sifre-sifirla?u={userId:N}&t={Uri.EscapeDataString(token)}";

    [HttpGet("eposta-dogrula")]
    public async Task<IResult> Verify([FromQuery(Name = "u")] Guid userId, [FromQuery(Name = "t")] string? token, CancellationToken ct)
    {
        if (!await _auth.ConfirmEmailAsync(userId, token ?? "", ct))
        {
            return PublicPages.Html("E-posta doğrulama", """
                <div class="card"><p>Bağlantı geçersiz ya da süresi dolmuş.</p>
                <p>Uygulamada Profil › Hesap ve güvenlik'ten yeni bağlantı isteyebilirsin.</p></div>
                """, StatusCodes.Status400BadRequest);
        }

        return PublicPages.Html("E-postan doğrulandı", """
            <div class="card"><p>Teşekkürler. Artık yorum yazabilir ve fotoğraf yükleyebilirsin. Uygulamaya dönebilirsin.</p></div>
            """);
    }

    [HttpGet("sifre-sifirla")]
    public IResult ResetForm([FromQuery(Name = "u")] Guid userId, [FromQuery(Name = "t")] string? token) =>
        PublicPages.Html(ResetTitle, Form(userId, token, null));

    [HttpPost("sifre-sifirla")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IResult> Reset(
        [FromForm(Name = "u")] Guid userId,
        [FromForm(Name = "t")] string? token,
        [FromForm(Name = "password")] string? password,
        [FromForm(Name = "confirm")] string? confirm,
        CancellationToken ct)
    {
        if (password != confirm)
        {
            return PublicPages.Html(ResetTitle, Form(userId, token, "Şifreler aynı değil."), StatusCodes.Status400BadRequest);
        }

        bool done;
        try
        {
            done = await _auth.ResetPasswordAsync(userId, token ?? "", password ?? "", ct);
        }
        catch (ValidationException ex)
        {
            return PublicPages.Html(ResetTitle, Form(userId, token, ex.Message), StatusCodes.Status400BadRequest);
        }

        if (!done)
        {
            return PublicPages.Html(ResetTitle, """
                <div class="card"><p>Bağlantı geçersiz, kullanılmış ya da süresi dolmuş. Şifren değişmedi.</p>
                <p>Uygulamada giriş ekranındaki "Şifremi unuttum" ile yeni bağlantı isteyebilirsin.</p></div>
                """, StatusCodes.Status400BadRequest);
        }

        return PublicPages.Html("Şifren değişti", """
            <div class="card"><p>Yeni şifrenle uygulamada giriş yapabilirsin. Güvenliğin için açık oturumların kapatıldı.</p></div>
            """);
    }

    private static string Form(Guid userId, string? token, string? error) => $"""
        {(error is null ? "" : $"<p class=\"note\">{PublicPages.Encode(error)}</p>")}
        <form method="post" action="sifre-sifirla" class="card">
          <input type="hidden" name="u" value="{PublicPages.Encode(userId.ToString("N"))}">
          <input type="hidden" name="t" value="{PublicPages.Encode(token)}">
          <label for="password">Yeni şifre</label>
          <input id="password" name="password" type="password" autocomplete="new-password" minlength="8" maxlength="128" required>
          <p class="muted">En az 8 karakter; en az bir harf ve bir rakam.</p>
          <label for="confirm">Yeni şifre (tekrar)</label>
          <input id="confirm" name="confirm" type="password" autocomplete="new-password" minlength="8" maxlength="128" required>
          <button type="submit">Şifremi değiştir</button>
        </form>
        """;
}
