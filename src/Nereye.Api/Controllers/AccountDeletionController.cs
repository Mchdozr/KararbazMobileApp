using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Auth;
using Nereye.Application.Common;

namespace Nereye.Api.Controllers;

/// <summary>
/// Uygulamayı kurmadan hesap silme (Play hesap silme şartı). Adres: <c>{API hostu}/hesap-sil</c>.
/// E-posta tek başına hiçbir şeyi silmez: kayıtlı adrese gelen onay bağlantısı ve sayfadaki düğme gerekir.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("hesap-sil")]
[EnableRateLimiting("auth")]
public sealed class AccountDeletionController : ControllerBase
{
    private const string Title = "Kararbaz hesabını sil";

    private const string WhatHappens = """
        <div class="card">
          <h2>Silinenler</h2>
          <ul>
            <li>Ad, e-posta, şifre ve profil fotoğrafın</li>
            <li>Kaydettiğin mekanlar, kayıtlı konumların (ev, iş) ve tercihlerin</li>
            <li>Yorumlarının metinleri ve yorumlarına eklediğin fotoğraf dosyaları</li>
            <li>Engellediğin ya da seni engelleyen kullanıcı kayıtları</li>
            <li>Tüm cihazlardaki oturumların</li>
          </ul>
          <h2>Tutulanlar</h2>
          <ul>
            <li>Yorumlarının yıldız puanı, kimliğin olmadan (mekan ortalaması bozulmasın diye)</li>
            <li>Verdiğin onayların kaydı ve yaptığın şikayetler, yasal yükümlülükler için kimliksiz olarak</li>
            <li>İşletme sahibiysen mekan sayfasına yüklediğin işletme fotoğrafları (mekanın içeriğidir)</li>
          </ul>
          <p class="muted">Silme geri alınamaz. Uygulamadan da silebilirsin: Profil › Hesap ve güvenlik › Hesabımı sil.</p>
        </div>
        """;

    private readonly IAuthService _auth;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public AccountDeletionController(IAuthService auth, IConfiguration config, IHostEnvironment env)
    {
        _auth = auth;
        _config = config;
        _env = env;
    }

    [HttpGet]
    public IResult Form() => PublicPages.Html(Title, """
        <p>Hesabına kayıtlı e-posta adresini yaz. O adrese bir onay bağlantısı göndereceğiz;
        hesabın, bağlantıyı açıp onayladığında silinir.</p>
        <form method="post" action="hesap-sil" class="card">
          <label for="email">E-posta</label>
          <input id="email" name="email" type="email" autocomplete="email" required>
          <button type="submit">Onay bağlantısı gönder</button>
        </form>
        """ + WhatHappens);

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IResult> SendLink([FromForm] string? email, CancellationToken ct)
    {
        var baseUrl = PublicPages.BaseUrl(HttpContext, _config, _env);
        if (baseUrl is null)
        {
            return Unavailable();
        }

        try
        {
            await _auth.RequestDeletionAsync(
                email ?? "",
                (userId, token) => $"{baseUrl}/hesap-sil/onay?u={userId:N}&t={Uri.EscapeDataString(token)}",
                ct);
        }
        catch (ServiceUnavailableException)
        {
            return Unavailable();
        }

        // Kayıtlı olsun olmasın aynı yanıt: hangi adresin kayıtlı olduğu bu sayfadan öğrenilemesin.
        return PublicPages.Html(Title, """
            <div class="card"><p>Bu adres bir Kararbaz hesabına kayıtlıysa, birkaç dakika içinde bir onay bağlantısı gelecek.
            Bağlantı 2 saat geçerlidir.</p><p class="muted">Gelen kutunda yoksa istenmeyen klasörüne bak.</p></div>
            """);
    }

    /// <summary>Bağlantı açılınca hemen silmez: e-posta tarayıcıları bağlantıyı önceden açabilir. Onay düğmesi ister.</summary>
    [HttpGet("onay")]
    public IResult Confirm([FromQuery(Name = "u")] Guid userId, [FromQuery(Name = "t")] string? token)
    {
        var u = PublicPages.Encode(userId.ToString("N"));
        var t = PublicPages.Encode(token);
        return PublicPages.Html(Title, $"""
            <p class="note">Bu işlem geri alınamaz.</p>
            {WhatHappens}
            <form method="post" action="onay" class="card">
              <input type="hidden" name="u" value="{u}">
              <input type="hidden" name="t" value="{t}">
              <button type="submit" class="danger">Hesabımı kalıcı olarak sil</button>
            </form>
            """);
    }

    [HttpPost("onay")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IResult> Delete([FromForm(Name = "u")] Guid userId, [FromForm(Name = "t")] string? token, CancellationToken ct)
    {
        if (!await _auth.ConfirmDeletionAsync(userId, token ?? "", ct))
        {
            return PublicPages.Html(Title, """
                <div class="card"><p>Bağlantı geçersiz ya da süresi dolmuş. Hesap silinmedi.</p>
                <p><a href="../hesap-sil">Yeni bağlantı iste</a></p></div>
                """, StatusCodes.Status400BadRequest);
        }

        return PublicPages.Html("Hesabın silindi", """
            <div class="card"><p>Kararbaz hesabın ve yukarıda listelenen verilerin silindi. Uygulamada açık oturumların kapatıldı.</p></div>
            """);
    }

    private static IResult Unavailable() => PublicPages.Html(Title, """
        <div class="card"><p>Hesap silme bağlantısı şu an gönderilemiyor.</p>
        <p>Hesabını uygulamadan hemen silebilirsin: Profil › Hesap ve güvenlik › Hesabımı sil.</p></div>
        """, StatusCodes.Status503ServiceUnavailable);
}
