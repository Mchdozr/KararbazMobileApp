using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Nereye.Api.Tests;

/// <summary>Production ortamında açılan API: sandbox gibi geliştirme yüzeyleri burada görünmemeli.</summary>
public sealed class ProductionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _db = "nereye-prod-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Nereye.Api", "Nereye.Api.csproj")))
        {
            dir = dir.Parent;
        }

        builder.UseContentRoot(Path.Combine(dir!.FullName, "src", "Nereye.Api"));
        builder.UseEnvironment("Production");
        // Program.cs bunları Build'den önce okur: UseSetting ile erken verilmeli.
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:Name", _db);
        builder.UseSetting("Redis:UseMemory", "true");
        builder.UseSetting("Places:UseFake", "true");
        builder.UseSetting("Jwt:SigningKey", "production-test-signing-key-0123456789abcdef");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:UseInMemory"] = "true",
            ["Database:Name"] = _db,
            ["Redis:UseMemory"] = "true",
            ["Places:UseFake"] = "true",
            // Yalnızca test için; dev-only olmayan, 32+ karakterlik anahtar.
            ["Jwt:SigningKey"] = "production-test-signing-key-0123456789abcdef"
        }));
    }
}

public sealed class ProductionSurfaceTests : IClassFixture<ProductionApiFactory>
{
    private readonly ProductionApiFactory _factory;

    public ProductionSurfaceTests(ProductionApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Sandbox_checkout_route_does_not_exist_in_production()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsync($"/api/promotions/{Guid.NewGuid()}/checkout", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Deletion_page_does_not_send_links_without_a_configured_public_url()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsync("/hesap-sil", new FormUrlEncodedContent([new("email", "a@b.co")]));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Contains("Hesabımı sil", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Password_reset_is_closed_without_a_provider_and_public_url()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/forgot", new { email = "a@b.co" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Contains("Şifre sıfırlama şu an kapalı.", await res.Content.ReadAsStringAsync());
    }
}

[Collection("api-flow")]
public sealed class PlayPolicyTests
{
    private readonly NereyeApiFactory _factory;

    public PlayPolicyTests(NereyeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid UserId, string Email)> Signup(string name = "Deneme", bool verified = true)
    {
        var client = _factory.CreateClient();
        var email = $"p{Guid.NewGuid():N}@nereye.test";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { acceptTerms = true, email, password = "kahve1234", displayName = name });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        var auth = JsonDocument.Parse(raw).RootElement;
        if (verified)
        {
            await _factory.VerifyEmailAsync(email);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, auth.GetProperty("userId").GetGuid(), email);
    }

    [Fact]
    public async Task Sandbox_checkout_still_exists_in_testing()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsync($"/api/promotions/{Guid.NewGuid()}/checkout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Registration_requires_accepting_the_terms()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"t{Guid.NewGuid():N}@nereye.test", password = "kahve1234", displayName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("kullanım koşullarını", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_blocked_users_review_is_hidden_from_the_blocker_only()
    {
        var (author, authorId, _) = await Signup("Kaba Yazar");
        var (reader, _, _) = await Signup("Okur");
        var (other, _, _) = await Signup("Başka");
        using (author)
        using (reader)
        using (other)
        {
            (await author.PostAsJsonAsync("/api/venues/nereye:goygoy/reviews", new { stars = 1, text = "Hakaret içeren yorum" })).EnsureSuccessStatusCode();
            Assert.Contains(await Ids(reader), id => id == authorId);

            Assert.Equal(HttpStatusCode.NoContent, (await reader.PostAsync($"/api/me/blocks/{authorId}", null)).StatusCode);
            Assert.DoesNotContain(await Ids(reader), id => id == authorId);
            Assert.Contains(await Ids(other), id => id == authorId);
            var blocks = JsonDocument.Parse(await reader.GetStringAsync("/api/me/blocks")).RootElement;
            Assert.Equal(authorId, blocks[0].GetProperty("userId").GetGuid());
            Assert.Equal("Kaba Yazar", blocks[0].GetProperty("displayName").GetString());

            Assert.Equal(HttpStatusCode.NoContent, (await reader.DeleteAsync($"/api/me/blocks/{authorId}")).StatusCode);
            Assert.Contains(await Ids(reader), id => id == authorId);
            Assert.Equal(HttpStatusCode.BadRequest, (await reader.PostAsync($"/api/me/blocks/{await MyId(reader)}", null)).StatusCode);
        }

        static async Task<List<Guid>> Ids(HttpClient c) =>
            JsonDocument.Parse(await c.GetStringAsync("/api/venues/nereye:goygoy/reviews")).RootElement
                .EnumerateArray().Select(r => r.GetProperty("userId").GetGuid()).ToList();

        static async Task<Guid> MyId(HttpClient c) =>
            JsonDocument.Parse(await c.GetStringAsync("/api/me")).RootElement.GetProperty("userId").GetGuid();
    }

    [Fact]
    public async Task A_review_photo_can_be_reported_but_foreign_urls_cannot()
    {
        var (client, _, _) = await Signup();
        using var _c = client;
        var ok = await client.PostAsJsonAsync("/api/venues/nereye:petra/reports",
            new { reason = "uygunsuz-fotograf", photoUrl = "/media/uploads/0123456789abcdef0123456789abcdef.jpg" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        var bad = await client.PostAsJsonAsync("/api/venues/nereye:petra/reports",
            new { reason = "uygunsuz-fotograf", photoUrl = "https://evil.example/x.jpg" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Deletion_page_without_the_emailed_link_deletes_nothing()
    {
        var (client, userId, email) = await Signup();
        client.Dispose();
        using var web = _factory.CreateClient();

        var form = await web.GetAsync("/hesap-sil");
        form.EnsureSuccessStatusCode();
        Assert.Contains("Tutulanlar", await form.Content.ReadAsStringAsync());

        var requested = await web.PostAsync("/hesap-sil", new FormUrlEncodedContent([new("email", email)]));
        requested.EnsureSuccessStatusCode();
        var unknown = await web.PostAsync("/hesap-sil", new FormUrlEncodedContent([new("email", "kimse-yok@nereye.test")]));
        Assert.Equal(await Body(requested), await Body(unknown));

        // Yalnızca e-posta ve yanlış token: hesap yerinde.
        var forged = await web.PostAsync("/hesap-sil/onay", new FormUrlEncodedContent([new("u", userId.ToString("N")), new("t", "uydurma")]));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).EnsureSuccessStatusCode();
    }

    private async Task<string> UploadPhoto(HttpClient client)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(MediaRoot(), "venues", "petra.jpg"));
        var upload = await client.PostAsJsonAsync("/api/me/photos", new { base64 = Convert.ToBase64String(bytes), contentType = "image/jpeg" });
        var raw = await upload.Content.ReadAsStringAsync();
        Assert.True(upload.IsSuccessStatusCode, raw);
        var url = JsonDocument.Parse(raw).RootElement.GetProperty("url").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        return url;
    }

    private string UploadFile(string url) => Path.Combine(MediaRoot(), "uploads", Path.GetFileName(url));

    private async Task<Guid> PostReview(HttpClient client, string placeId, params string[] photos)
    {
        var res = await client.PostAsJsonAsync($"/api/venues/{placeId}/reviews", new { stars = 4, text = "Fotoğraflı yorum", photoUrls = photos });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        return JsonDocument.Parse(raw).RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Deleting_a_review_removes_its_photo_files()
    {
        var (client, _, _) = await Signup();
        using var _c = client;
        var url = await UploadPhoto(client);
        var id = await PostReview(client, "nereye:moda", url);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/venues/reviews/{id}")).StatusCode);
        Assert.False(File.Exists(UploadFile(url)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Replacing_a_review_photo_removes_the_old_file_and_keeps_the_new_one()
    {
        var (client, _, _) = await Signup();
        using var _c = client;
        var oldUrl = await UploadPhoto(client);
        var keptUrl = await UploadPhoto(client);
        await PostReview(client, "nereye:modern", oldUrl, keptUrl);
        var newUrl = await UploadPhoto(client);
        await PostReview(client, "nereye:modern", keptUrl, newUrl);

        Assert.False(File.Exists(UploadFile(oldUrl)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(oldUrl)).StatusCode);
        Assert.True(File.Exists(UploadFile(keptUrl)));
        Assert.True(File.Exists(UploadFile(newUrl)));
        // Uzun süreli/immutable önbellek yok: silinen kare önbellekte yaşamasın.
        var cache = (await client.GetAsync(newUrl)).Headers.CacheControl!;
        Assert.True(cache.NoCache);
        Assert.Null(cache.MaxAge);
    }

    [Fact]
    public async Task Hiding_a_reported_photo_deletes_the_file_and_admin_sees_the_photo_url()
    {
        var (author, _, _) = await Signup("Fotoğrafçı");
        var (reporter, _, _) = await Signup("Şikayetçi");
        using var _a = author;
        using var _r = reporter;
        var url = await UploadPhoto(author);
        var reviewId = await PostReview(author, "nereye:montag", url);
        (await reporter.PostAsJsonAsync("/api/venues/nereye:montag/reports", new { reason = "Uygunsuz fotoğraf", reviewId, photoUrl = url }))
            .EnsureSuccessStatusCode();

        using var admin = _factory.CreateClient();
        var login = JsonDocument.Parse(await (await admin.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@nereye.app", password = "Admin!12345" })).Content.ReadAsStringAsync()).RootElement;
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        var reports = JsonDocument.Parse(await admin.GetStringAsync("/api/admin/reports")).RootElement;
        var report = reports.EnumerateArray().Single(r => r.GetProperty("photoUrl").GetString() == url);

        var hide = await admin.PostAsync($"/api/admin/reports/{report.GetProperty("id").GetGuid()}/resolve?hideReview=true", null);
        Assert.True(hide.IsSuccessStatusCode, await hide.Content.ReadAsStringAsync());
        Assert.False(File.Exists(UploadFile(url)));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
        var listed = JsonDocument.Parse(await admin.GetStringAsync("/api/venues/nereye:montag/reviews")).RootElement;
        Assert.DoesNotContain(listed.EnumerateArray().SelectMany(r => r.GetProperty("photoUrls").EnumerateArray()), p => p.GetString() == url);
    }

    [Fact]
    public async Task The_emailed_link_deletes_the_account_and_its_review_photo_files()
    {
        var (client, userId, email) = await Signup();
        using var _c = client;
        var bytes = await File.ReadAllBytesAsync(Path.Combine(MediaRoot(), "venues", "petra.jpg"));
        var upload = await client.PostAsJsonAsync("/api/me/photos", new { base64 = Convert.ToBase64String(bytes), contentType = "image/jpeg" });
        var raw = await upload.Content.ReadAsStringAsync();
        Assert.True(upload.IsSuccessStatusCode, raw);
        var url = JsonDocument.Parse(raw).RootElement.GetProperty("url").GetString()!;
        var file = Path.Combine(MediaRoot(), "uploads", Path.GetFileName(url));
        Assert.True(File.Exists(file));
        (await client.PostAsJsonAsync("/api/venues/nereye:walter/reviews", new { stars = 4, text = "Silinecek yorum", photoUrls = new[] { url } }))
            .EnsureSuccessStatusCode();

        using var web = _factory.CreateClient();
        (await web.PostAsync("/hesap-sil", new FormUrlEncodedContent([new("email", email)]))).EnsureSuccessStatusCode();
        var link = _factory.Mail.LastLinkTo(email);
        Assert.NotNull(link);
        var query = new Uri(link!).Query;

        // Bağlantıyı açmak tek başına silmez (e-posta tarayıcıları önceden açabilir).
        var page = await web.GetAsync("/hesap-sil/onay" + query);
        page.EnsureSuccessStatusCode();
        (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).EnsureSuccessStatusCode();

        var html = await page.Content.ReadAsStringAsync();
        var u = Regex.Match(html, "name=\"u\" value=\"([^\"]+)\"").Groups[1].Value;
        var t = WebUtility.HtmlDecode(Regex.Match(html, "name=\"t\" value=\"([^\"]+)\"").Groups[1].Value);
        var done = await web.PostAsync("/hesap-sil/onay", new FormUrlEncodedContent([new("u", u), new("t", t)]));
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        Assert.False(File.Exists(file));
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).StatusCode);
        var reviews = JsonDocument.Parse(await web.GetStringAsync("/api/venues/nereye:walter/reviews")).RootElement;
        var mine = reviews.EnumerateArray().Single(r => r.GetProperty("userId").GetGuid() == userId);
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("text").ValueKind);
        Assert.Equal(0, mine.GetProperty("photoUrls").GetArrayLength());

        // Aynı bağlantı ikinci kez işe yaramaz.
        var again = await web.PostAsync("/hesap-sil/onay", new FormUrlEncodedContent([new("u", u), new("t", t)]));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    private static async Task<string> Body(HttpResponseMessage res) => await res.Content.ReadAsStringAsync();

    private static string MediaRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Nereye.Api", "wwwroot", "media")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "src", "Nereye.Api", "wwwroot", "media");
    }
}
