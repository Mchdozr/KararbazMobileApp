using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Nereye.Api.Tests;

/// <summary>Şifre sıfırlama bağlantısının ömrü çok kısa: süresi dolmuş token'ı denemek için.</summary>
public sealed class ShortTokenApiFactory : NereyeApiFactory
{
    protected override void ConfigureExtraServices(IServiceCollection services) =>
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromMilliseconds(1));
}

/// <summary>E-posta sağlayıcısı tanımlı değil.</summary>
public sealed class NoEmailApiFactory : NereyeApiFactory
{
    public NoEmailApiFactory() => Mail.IsConfigured = false;
}

internal static class EmailFlow
{
    public static async Task<(HttpClient Client, Guid UserId, string Email, string Refresh)> Signup(NereyeApiFactory factory, string password = "kahve1234")
    {
        var client = factory.CreateClient();
        var email = $"m{Guid.NewGuid():N}@nereye.test";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { acceptTerms = true, email, password, displayName = "Posta" });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        var auth = JsonDocument.Parse(raw).RootElement;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, auth.GetProperty("userId").GetGuid(), email, auth.GetProperty("refreshToken").GetString()!);
    }

    public static string ResetLink(NereyeApiFactory factory, string email)
    {
        var mail = factory.Mail.Sent.Last(m => m.To == email && m.Text.Contains("/sifre-sifirla?", StringComparison.Ordinal));
        return new Uri(Regex.Match(mail.Text, @"https?://\S+").Value).PathAndQuery;
    }

    /// <summary>Sıfırlama sayfasındaki formu tarayıcı gibi gönderir.</summary>
    public static async Task<HttpResponseMessage> SubmitReset(HttpClient web, string pathAndQuery, string password)
    {
        var page = await web.GetStringAsync(pathAndQuery);
        string Hidden(string name) => WebUtility.HtmlDecode(Regex.Match(page, $"name=\"{name}\" value=\"([^\"]*)\"").Groups[1].Value);
        return await web.PostAsync("/sifre-sifirla", new FormUrlEncodedContent(
        [
            new("u", Hidden("u")), new("t", Hidden("t")), new("password", password), new("confirm", password)
        ]));
    }
}

[Collection("api-flow")]
public sealed class EmailFlowTests
{
    private readonly NereyeApiFactory _factory;

    public EmailFlowTests(NereyeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Forgot_answers_the_same_for_registered_and_unknown_addresses()
    {
        var (client, _, email, _) = await EmailFlow.Signup(_factory);
        client.Dispose();
        using var anon = _factory.CreateClient();
        var before = _factory.Mail.Sent.Count;

        var known = await anon.PostAsJsonAsync("/api/auth/forgot", new { email = email.ToUpperInvariant() });
        var unknown = await anon.PostAsJsonAsync("/api/auth/forgot", new { email = $"yok{Guid.NewGuid():N}@nereye.test" });
        var garbage = await anon.PostAsJsonAsync("/api/auth/forgot", new { email = "eposta-degil" });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(known.StatusCode, garbage.StatusCode);
        var body = await known.Content.ReadAsStringAsync();
        Assert.Equal(body, await unknown.Content.ReadAsStringAsync());
        Assert.Equal(body, await garbage.Content.ReadAsStringAsync());
        // Yalnızca kayıtlı adrese mektup gider.
        Assert.Equal(before + 1, _factory.Mail.Sent.Count);
        Assert.Equal(email, _factory.Mail.Sent[^1].To);
    }

    [Fact]
    public async Task Reset_link_works_once_and_closes_every_session()
    {
        var (client, _, email, refresh) = await EmailFlow.Signup(_factory);
        client.Dispose();
        using var web = _factory.CreateClient();
        (await web.PostAsJsonAsync("/api/auth/forgot", new { email })).EnsureSuccessStatusCode();
        var link = EmailFlow.ResetLink(_factory, email);
        Assert.StartsWith("/sifre-sifirla?u=", link);

        var mismatch = await web.PostAsync("/sifre-sifirla", new FormUrlEncodedContent([new("u", "x"), new("t", "y"), new("password", "a"), new("confirm", "b")]));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        var weak = await EmailFlow.SubmitReset(web, link, "kisa");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Contains("en az 8 karakter", await weak.Content.ReadAsStringAsync());

        var ok = await EmailFlow.SubmitReset(web, link, "yenisifre42");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("Şifren değişti", await ok.Content.ReadAsStringAsync());

        var again = await EmailFlow.SubmitReset(web, link, "baskasifre77");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("Şifren değişmedi", await again.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await web.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "baskasifre77" })).StatusCode);
        (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "yenisifre42" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Unverified_account_can_log_in_but_cannot_write_reviews_or_photos()
    {
        var (client, _, email, _) = await EmailFlow.Signup(_factory);
        using var _c = client;
        using var anon = _factory.CreateClient();
        (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).EnsureSuccessStatusCode();
        var profile = JsonDocument.Parse(await client.GetStringAsync("/api/me")).RootElement;
        Assert.False(profile.GetProperty("emailVerified").GetBoolean());

        var review = await client.PostAsJsonAsync("/api/venues/nereye:ciya/reviews", new { stars = 4, text = "Güzel" });
        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        Assert.Contains("e-posta adresini doğrula", await review.Content.ReadAsStringAsync());
        var photo = await client.PostAsJsonAsync("/api/me/photos", new { base64 = "AAAA", contentType = "image/jpeg" });
        Assert.Equal(HttpStatusCode.Forbidden, photo.StatusCode);
        var avatar = await client.PostAsJsonAsync("/api/me/avatar", new { base64 = "AAAA", contentType = "image/jpeg" });
        Assert.Equal(HttpStatusCode.Forbidden, avatar.StatusCode);

        // Yeniden gönderme yeni bir doğrulama mektubu üretir; bağlantı açılınca yazma açılır.
        var resend = await client.PostAsync("/api/auth/verify-email", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.True(JsonDocument.Parse(await resend.Content.ReadAsStringAsync()).RootElement.GetProperty("sent").GetBoolean());
        await _factory.VerifyEmailAsync(email);
        profile = JsonDocument.Parse(await client.GetStringAsync("/api/me")).RootElement;
        Assert.True(profile.GetProperty("emailVerified").GetBoolean());
        var written = await client.PostAsJsonAsync("/api/venues/nereye:ciya/reviews", new { stars = 4, text = "Güzel" });
        Assert.True(written.IsSuccessStatusCode, await written.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Resend_on_a_verified_account_says_so_and_sends_nothing()
    {
        var (client, _, email, _) = await EmailFlow.Signup(_factory);
        using var _c = client;
        await _factory.VerifyEmailAsync(email);
        var before = _factory.Mail.Sent.Count;

        var res = await client.PostAsync("/api/auth/verify-email", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.GetProperty("sent").GetBoolean());
        Assert.True(body.GetProperty("alreadyVerified").GetBoolean());
        Assert.Equal(before, _factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task A_forged_verification_link_does_not_verify()
    {
        var (client, userId, _, _) = await EmailFlow.Signup(_factory);
        using var _c = client;
        var res = await client.GetAsync($"/eposta-dogrula?u={userId:N}&t=sahte");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var profile = JsonDocument.Parse(await client.GetStringAsync("/api/me")).RootElement;
        Assert.False(profile.GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    public async Task Legal_text_is_served_from_the_api_without_an_invented_contact()
    {
        using var client = _factory.CreateClient();
        var raw = await client.GetStringAsync("/api/legal");
        var legal = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(JsonValueKind.Null, legal.GetProperty("contact").ValueKind);
        Assert.Contains("İletişim adresi tanımlanmadı.", raw);
        Assert.DoesNotContain("@nereye", raw);
        Assert.True(legal.GetProperty("privacy").GetProperty("sections").GetArrayLength() >= 5);
        Assert.True(legal.GetProperty("terms").GetProperty("sections").GetArrayLength() >= 3);

        var page = await client.GetAsync("/gizlilik");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Gizlilik ve KVKK", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/kosullar")).StatusCode);
    }
}

public sealed class ExpiredResetLinkTests : IClassFixture<ShortTokenApiFactory>
{
    private readonly ShortTokenApiFactory _factory;

    public ExpiredResetLinkTests(ShortTokenApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_expired_reset_link_does_not_change_the_password()
    {
        var (client, _, email, _) = await EmailFlow.Signup(_factory);
        client.Dispose();
        using var web = _factory.CreateClient();
        (await web.PostAsJsonAsync("/api/auth/forgot", new { email })).EnsureSuccessStatusCode();
        var link = EmailFlow.ResetLink(_factory, email);
        await Task.Delay(50);

        var res = await EmailFlow.SubmitReset(web, link, "yenisifre42");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).EnsureSuccessStatusCode();
    }
}

public sealed class NoEmailProviderTests : IClassFixture<NoEmailApiFactory>
{
    private readonly NoEmailApiFactory _factory;

    public NoEmailProviderTests(NoEmailApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Without_a_provider_reset_is_closed_with_503_and_registration_still_works()
    {
        var (client, _, _, _) = await EmailFlow.Signup(_factory);
        using var _c = client;
        Assert.Empty(_factory.Mail.Sent);

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot", new { email = "biri@nereye.test" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, forgot.StatusCode);
        Assert.Contains("Şifre sıfırlama şu an kapalı.", await forgot.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/auth/verify-email", null)).StatusCode);
    }
}
