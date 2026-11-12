using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nereye.Api.Tests;

[Collection("api-flow")]
public sealed class AccountAndReviewTests
{
    private readonly NereyeApiFactory _factory;

    public AccountAndReviewTests(NereyeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, JsonElement Auth, string Email)> Signup(string name = "Deneme", string password = "kahve1234")
    {
        var client = _factory.CreateClient();
        var email = $"t{Guid.NewGuid():N}@nereye.test";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = name });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, raw);
        var auth = JsonDocument.Parse(raw).RootElement;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, auth, email);
    }

    private static async Task<string> Error(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("error").GetString()!;

    [Fact]
    public async Task Password_without_symbol_or_uppercase_is_accepted()
    {
        var (client, auth, _) = await Signup(password: "kahve1234");
        Assert.False(string.IsNullOrEmpty(auth.GetProperty("refreshToken").GetString()));
        client.Dispose();
    }

    [Fact]
    public async Task Weak_password_and_duplicate_email_explain_themselves()
    {
        using var client = _factory.CreateClient();
        var email = $"w{Guid.NewGuid():N}@nereye.test";
        var weak = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "kisa", displayName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Contains("8 karakter", await Error(weak));

        var bad = await client.PostAsJsonAsync("/api/auth/register", new { email = "eposta-degil", password = "kahve1234", displayName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("e-posta", await Error(bad));

        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "kahve1234", displayName = "A" })).EnsureSuccessStatusCode();
        var dup = await client.PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = "kahve1234", displayName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Contains("zaten kayıtlı", await Error(dup));
    }

    [Fact]
    public async Task Login_is_case_insensitive_and_locks_after_repeated_failures()
    {
        var (owner, _, email) = await Signup();
        owner.Dispose();
        using var client = _factory.CreateClient();

        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email = "  " + email.ToUpperInvariant(), password = "kahve1234" });
        ok.EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis999" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_look_the_same()
    {
        var (owner, _, email) = await Signup();
        owner.Dispose();
        using var client = _factory.CreateClient();
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = "yok@nereye.test", password = "kahve1234" });
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis999" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await Error(unknown), await Error(wrong));
    }

    [Fact]
    public async Task Profile_can_be_read_and_renamed()
    {
        var (client, _, email) = await Signup("İlk Ad");
        using var _c = client;

        var me = JsonDocument.Parse(await client.GetStringAsync("/api/me")).RootElement;
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("İlk Ad", me.GetProperty("displayName").GetString());
        Assert.Equal(0, me.GetProperty("reviewCount").GetInt32());

        var renamed = await client.PutAsJsonAsync("/api/me", new { displayName = "  Yeni   Ad " });
        renamed.EnsureSuccessStatusCode();
        Assert.Equal("Yeni Ad", JsonDocument.Parse(await renamed.Content.ReadAsStringAsync()).RootElement.GetProperty("displayName").GetString());

        var empty = await client.PutAsJsonAsync("/api/me", new { displayName = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions()
    {
        var (client, auth, email) = await Signup();
        using var _c = client;
        var oldRefresh = auth.GetProperty("refreshToken").GetString();

        var wrong = await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "yanlis999", newPassword = "yenisifre42" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("Mevcut şifre", await Error(wrong));

        var changed = await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "kahve1234", newPassword = "yenisifre42" });
        changed.EnsureSuccessStatusCode();

        using var anon = _factory.CreateClient();
        var stale = await anon.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).StatusCode);
        (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "yenisifre42" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Second_review_updates_the_first_and_can_be_deleted()
    {
        var (client, auth, _) = await Signup();
        using var _c = client;
        var me = auth.GetProperty("userId").GetGuid();

        var first = await client.PostAsJsonAsync("/api/venues/nereye:fazil/reviews",
            new { stars = 2, text = "Fena değil", tags = new[] { "Sessiz", "Laptop" }, photoUrls = Array.Empty<string>() });
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var second = await client.PostAsJsonAsync("/api/venues/nereye:fazil/reviews",
            new { stars = 5, text = "  Tekrar gittim, çok iyi  ", tags = new[] { "Sessiz", "Romantik" } });
        Assert.True(second.IsSuccessStatusCode, await second.Content.ReadAsStringAsync());

        var list = JsonDocument.Parse(await client.GetStringAsync("/api/venues/nereye:fazil/reviews")).RootElement;
        var mine = list.EnumerateArray().Where(r => r.GetProperty("userId").GetGuid() == me).ToList();
        Assert.Single(mine);
        Assert.Equal(5, mine[0].GetProperty("stars").GetInt32());
        Assert.Equal("Tekrar gittim, çok iyi", mine[0].GetProperty("text").GetString());
        Assert.Equal(["Romantik", "Sessiz"], mine[0].GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).Order().ToArray());

        var venue = JsonDocument.Parse(await client.GetStringAsync("/api/venues/nereye:fazil")).RootElement.GetProperty("nereye");
        Assert.Equal(1, venue.GetProperty("ratingCount").GetInt32());
        Assert.Equal(5, venue.GetProperty("ratingAvg").GetDouble());

        var myReviews = JsonDocument.Parse(await client.GetStringAsync("/api/me/reviews")).RootElement;
        Assert.Equal("nereye:fazil", myReviews[0].GetProperty("placeId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(myReviews[0].GetProperty("venueName").GetString()));

        var (other, _, _) = await Signup();
        using var _o = other;
        var id = mine[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/venues/reviews/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/venues/reviews/{id}")).StatusCode);
        var after = JsonDocument.Parse(await client.GetStringAsync("/api/venues/nereye:fazil")).RootElement.GetProperty("nereye");
        Assert.Equal(0, after.GetProperty("ratingCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/venues/reviews/{id}")).StatusCode);
    }

    [Fact]
    public async Task Review_rejects_bad_stars_and_foreign_photo_urls()
    {
        var (client, _, _) = await Signup();
        using var _c = client;

        var stars = await client.PostAsJsonAsync("/api/venues/nereye:petra/reviews", new { stars = 9, text = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, stars.StatusCode);
        Assert.Contains("1-5", await Error(stars));

        var photo = await client.PostAsJsonAsync("/api/venues/nereye:petra/reviews",
            new { stars = 4, text = "x", photoUrls = new[] { "https://evil.example/pixel.png" } });
        Assert.Equal(HttpStatusCode.BadRequest, photo.StatusCode);

        var missing = await client.PostAsJsonAsync("/api/venues/nereye:boyle-bir-yer-yok/reviews", new { stars = 4 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Bad_coordinates_and_unknown_vibes_do_not_500()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/recommendations?lat=999&lng=29&intent=Kahve")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/recommendations?intent=Kahve")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/routes?fromLat=0&fromLng=0&toLat=41&toLng=29")).StatusCode);

        var vibes = await client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&vibes=Sessiz,BoyleBirSeyYok&take=9999&skip=-5");
        Assert.True(vibes.IsSuccessStatusCode, await vibes.Content.ReadAsStringAsync());

        var search = await client.GetAsync("/api/search?lat=40.9885&lng=29.0250");
        search.EnsureSuccessStatusCode();
        Assert.Equal("[]", await search.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Consent_is_whitelisted_and_readable()
    {
        var (client, _, _) = await Signup();
        using var _c = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/me/consent", new { purpose = "her-sey", granted = true })).StatusCode);
        (await client.PostAsJsonAsync("/api/me/consent", new { purpose = "marketing", granted = true })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/me/consent", new { purpose = "marketing", granted = false })).EnsureSuccessStatusCode();
        var consents = JsonDocument.Parse(await client.GetStringAsync("/api/me/consents")).RootElement;
        Assert.False(consents.GetProperty("marketing").GetBoolean());
    }

    [Fact]
    public async Task Deleted_account_cannot_refresh_or_log_in()
    {
        var (client, auth, email) = await Signup();
        using var _c = client;
        (await client.PostAsync("/api/venues/nereye:petra/favorite", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/auth/me")).StatusCode);

        using var anon = _factory.CreateClient();
        var refresh = await anon.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).StatusCode);
    }

    [Fact]
    public async Task Errors_share_one_shape_and_carry_security_headers()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/auth/login", new StringContent("{ bozuk", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await Error(res)));
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());

        var live = await client.GetAsync("/health/live");
        live.EnsureSuccessStatusCode();
    }
}
