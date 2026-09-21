using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Nereye.Api.Tests;

public sealed class NereyeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "nereye-tests-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var api = Path.Combine(dir.FullName, "src", "Nereye.Api");
            if (File.Exists(Path.Combine(api, "Nereye.Api.csproj")))
            {
                builder.UseContentRoot(api);
                break;
            }

            dir = dir.Parent;
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:Name", _dbName);
        builder.UseSetting("Redis:UseMemory", "true");
        builder.UseSetting("Places:UseFake", "true");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:UseInMemory"] = "true",
                ["Database:Name"] = _dbName,
                ["Redis:UseMemory"] = "true",
                ["Places:UseFake"] = "true",
                ["Jwt:Issuer"] = "nereye",
                ["Jwt:Audience"] = "nereye-app",
                ["Jwt:SigningKey"] = "dev-only-change-me-32-chars-minimum!!"
            });
        });
    }
}

[CollectionDefinition("api-flow", DisableParallelization = true)]
public sealed class ApiFlowCollection : ICollectionFixture<NereyeApiFactory>;

[Collection("api-flow")]
public sealed class ApiFlowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NereyeApiFactory _factory;
    private readonly HttpClient _client;

    public ApiFlowTests(NereyeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Kadikoy_kahve_returns_at_least_five_cards()
    {
        var response = await _client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 5, items.ToString());
        var photo = items[0].GetProperty("photoUrl").GetString();
        Assert.Contains("/media/venues/", photo);
        var fileName = Path.GetFileName(photo);
        Assert.True(File.Exists(Path.Combine(VenuesMediaDir(), fileName!)), fileName);
        var petra = items.EnumerateArray().First(i => i.GetProperty("placeId").GetString() == "nereye:petra");
        Assert.Contains("çekirdek", petra.GetProperty("note").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenNow_excludes_closed_opera_kahve()
    {
        var all = await _client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500");
        all.EnsureSuccessStatusCode();
        var filtered = await _client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500&openNow=true");
        filtered.EnsureSuccessStatusCode();

        using var allDoc = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        using var openDoc = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        var allIds = Ids(allDoc);
        var openIds = Ids(openDoc);
        Assert.Contains("nereye:opera", allIds);
        Assert.DoesNotContain("nereye:opera", openIds);
    }

    [Fact]
    public async Task Register_review_favorite_and_preferences_work()
    {
        var email = $"u{Guid.NewGuid():N}@nereye.test";
        var auth = await Post<JsonElement>("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Test"
        });
        var token = auth.GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var detail = await _client.GetAsync("/api/venues/nereye:petra");
        detail.EnsureSuccessStatusCode();

        var review = await Post<JsonElement>("/api/venues/nereye:petra/reviews", new
        {
            stars = 5,
            text = "Harika kahve",
            tags = new[] { "Laptop" },
            photoUrls = Array.Empty<string>()
        });
        Assert.Equal(5, review.GetProperty("stars").GetInt32());

        var fav = await _client.PostAsync("/api/venues/nereye:petra/favorite", null);
        fav.EnsureSuccessStatusCode();

        var pref = await Put<JsonElement>("/api/me/preferences", new
        {
            defaultRadiusMeters = 1200,
            intents = new[] { "Kahve" },
            vibes = new[] { "Laptop", "Sessiz" }
        });
        Assert.Equal(1200, pref.GetProperty("defaultRadiusMeters").GetInt32());
    }

    [Fact]
    public async Task Favorite_then_list_includes_petra()
    {
        using var client = _factory.CreateClient();
        var email = $"fav{Guid.NewGuid():N}@nereye.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Fav"
        });
        register.EnsureSuccessStatusCode();
        using var auth = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());

        var fav = await client.PostAsync("/api/venues/nereye:petra/favorite", null);
        fav.EnsureSuccessStatusCode();

        var list = await client.GetAsync("/api/me/favorites");
        var raw = await list.Content.ReadAsStringAsync();
        Assert.True(list.IsSuccessStatusCode, raw);
        using var doc = JsonDocument.Parse(raw);
        var ids = doc.RootElement.EnumerateArray().Select(i => i.GetProperty("placeId").GetString()).ToList();
        Assert.Contains("nereye:petra", ids);
        Assert.Contains("/media/venues/", doc.RootElement[0].GetProperty("photoUrl").GetString());
    }

    [Fact]
    public async Task Google_photo_proxy_rejects_bad_name()
    {
        using var client = _factory.CreateClient();
        var bad = await client.GetAsync("/api/media/google?name=http://evil");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
        var missing = await client.GetAsync("/api/media/google?name=places/abc/photos/xyz");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Kultur_kadikoy_includes_sureyya()
    {
        var response = await _client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kultur&radiusMeters=1500");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = Ids(doc);
        Assert.Contains("nereye:sureyya", ids);
    }

    [Fact]
    public async Task Venue_photos_exist_and_are_served()
    {
        var dir = VenuesMediaDir();
        string[] ids =
        [
            "petra", "fazil", "walter", "montag", "federica", "goygoy", "opera", "mandabatmaz",
            "kronotrop", "ciya", "karakoy", "sureyya", "zorlu", "modern", "mini", "moda"
        ];
        foreach (var id in ids)
        {
            Assert.True(File.Exists(Path.Combine(dir, $"{id}.jpg")), id);
            var res = await _client.GetAsync($"/media/venues/{id}.jpg");
            Assert.True(res.IsSuccessStatusCode, $"{id} {(int)res.StatusCode}");
        }
    }

    [Fact]
    public async Task Far_from_kadikoy_does_not_list_petra()
    {
        var response = await _client.GetAsync("/api/recommendations?lat=41.0550&lng=28.9870&intent=Kahve&radiusMeters=1500");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = Ids(doc);
        Assert.DoesNotContain("nereye:petra", ids);
    }

    [Fact]
    public async Task Vibe_query_does_not_500()
    {
        var response = await _client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&vibes=Laptop");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Kultur_venue_includes_upcoming_event()
    {
        var response = await _client.GetAsync("/api/venues/nereye:sureyya");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("events").GetArrayLength() >= 1);
        Assert.Contains("Süreyya", doc.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Preferences_without_token_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/me/preferences");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Favorites_without_token_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/me/favorites");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Recommendations_with_invalid_bearer_still_succeed()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var response = await client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refresh_issues_new_access_token_for_preferences()
    {
        using var client = _factory.CreateClient();
        var email = $"r{Guid.NewGuid():N}@nereye.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Refresh"
        });
        register.EnsureSuccessStatusCode();
        using var authDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var refreshToken = authDoc.RootElement.GetProperty("refreshToken").GetString();
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        var raw = await refresh.Content.ReadAsStringAsync();
        Assert.True(refresh.IsSuccessStatusCode, raw);
        using var next = JsonDocument.Parse(raw);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", next.RootElement.GetProperty("accessToken").GetString());
        var prefs = await client.GetAsync("/api/me/preferences");
        Assert.True(prefs.IsSuccessStatusCode, await prefs.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Claim_approve_and_sandbox_checkout()
    {
        using var factory = new NereyeApiFactory();
        using var client = factory.CreateClient();
        var email = $"owner{Guid.NewGuid():N}@nereye.test";
        async Task<JsonElement> Post(string url, object body)
        {
            var response = await client.PostAsJsonAsync(url, body);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, raw);
            return JsonSerializer.Deserialize<JsonElement>(raw, Json);
        }

        var ownerAuth = await Post("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Owner"
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerAuth.GetProperty("accessToken").GetString());

        var claim = await Post("/api/venues/nereye:goygoy/claims", new { message = "Ben işletmeciyim" });
        var claimId = claim.GetProperty("id").GetGuid();

        var adminLogin = await Post("/api/auth/login", new
        {
            email = "admin@nereye.app",
            password = "Admin!12345"
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminLogin.GetProperty("accessToken").GetString());
        var approve = await client.PostAsync($"/api/admin/claims/{claimId}/approve", null);
        approve.EnsureSuccessStatusCode();

        var ownerRelogin = await Post("/api/auth/login", new
        {
            email,
            password = "Passw0rd!"
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerRelogin.GetProperty("accessToken").GetString());

        var campaign = await Post("/api/promotions", new
        {
            placeId = "nereye:goygoy",
            targetIntent = "Kahve",
            targetGeohash = "sxk9",
            days = 7,
            dailyBudget = 100
        });
        var campaignId = campaign.GetProperty("id").GetGuid();
        var paid = await Post($"/api/promotions/{campaignId}/checkout", new { });
        Assert.Equal("Active", paid.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Saved_place_geo_suggest_and_avatar_work()
    {
        using var client = _factory.CreateClient();
        var email = $"loc{Guid.NewGuid():N}@nereye.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Lok"
        });
        register.EnsureSuccessStatusCode();
        using var auth = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        Assert.True(auth.RootElement.TryGetProperty("avatarUrl", out var avatarProp));
        Assert.Equal(JsonValueKind.Null, avatarProp.ValueKind);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());

        var suggest = await client.GetAsync("/api/geo/suggest?q=Halide");
        suggest.EnsureSuccessStatusCode();
        using var hits = JsonDocument.Parse(await suggest.Content.ReadAsStringAsync());
        Assert.Contains(hits.RootElement.EnumerateArray(), h => h.GetProperty("label").GetString()!.Contains("Halide", StringComparison.OrdinalIgnoreCase));

        var add = await client.PostAsJsonAsync("/api/me/places", new
        {
            label = "Ev",
            kind = "home",
            latitude = 41.055,
            longitude = 28.987
        });
        var addRaw = await add.Content.ReadAsStringAsync();
        Assert.True(add.IsSuccessStatusCode, addRaw);
        using var saved = JsonDocument.Parse(addRaw);
        Assert.Equal("home", saved.RootElement.GetProperty("kind").GetString());

        var list = await client.GetAsync("/api/me/places");
        list.EnsureSuccessStatusCode();
        using var places = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, places.RootElement.GetArrayLength());

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }
            .Concat(new byte[48]).ToArray();
        var up = await client.PostAsJsonAsync("/api/me/avatar", new
        {
            base64 = Convert.ToBase64String(jpeg),
            contentType = "image/jpeg"
        });
        var upRaw = await up.Content.ReadAsStringAsync();
        Assert.True(up.IsSuccessStatusCode, upRaw);
        using var upDoc = JsonDocument.Parse(upRaw);
        var url = upDoc.RootElement.GetProperty("avatarUrl").GetString();
        Assert.StartsWith("/media/avatars/", url);
        var photo = await client.GetAsync(url);
        Assert.True(photo.IsSuccessStatusCode, $"{url} {(int)photo.StatusCode}");
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Guest_can_open_recommendations()
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500");
        res.EnsureSuccessStatusCode();
        Assert.True(res.Headers.Contains("X-Request-Id") || res.Headers.TryGetValues("X-Request-Id", out _));
    }

    [Fact]
    public async Task Consent_report_and_favorite_flag()
    {
        using var client = _factory.CreateClient();
        var email = $"c{Guid.NewGuid():N}@nereye.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Consent",
            acceptPrivacy = true
        });
        register.EnsureSuccessStatusCode();
        using var auth = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());

        var consent = await client.PostAsJsonAsync("/api/me/consent", new { purpose = "location", granted = true });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, consent.StatusCode);

        var report = await client.PostAsJsonAsync("/api/venues/nereye:petra/reports", new { reason = "yanlis-bilgi" });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, report.StatusCode);

        var fav = await client.PostAsync("/api/venues/nereye:petra/favorite", null);
        fav.EnsureSuccessStatusCode();
        var flag = await client.GetAsync("/api/venues/nereye:petra/favorite");
        flag.EnsureSuccessStatusCode();
        using var flagDoc = JsonDocument.Parse(await flag.Content.ReadAsStringAsync());
        Assert.True(flagDoc.RootElement.GetProperty("saved").GetBoolean());
    }

    [Fact]
    public async Task Admin_can_reject_claim()
    {
        using var client = _factory.CreateClient();
        var email = $"own{Guid.NewGuid():N}@nereye.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            displayName = "Owner"
        });
        register.EnsureSuccessStatusCode();
        using var auth = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());
        var claim = await client.PostAsJsonAsync("/api/venues/nereye:petra/claims", new { message = "Benim" });
        claim.EnsureSuccessStatusCode();
        using var claimDoc = JsonDocument.Parse(await claim.Content.ReadAsStringAsync());
        var claimId = claimDoc.RootElement.GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@nereye.app", password = "Admin!12345" });
        login.EnsureSuccessStatusCode();
        using var admin = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.RootElement.GetProperty("accessToken").GetString());
        var reject = await client.PostAsync($"/api/admin/claims/{claimId}/reject", null);
        Assert.True(reject.IsSuccessStatusCode, await reject.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> Post<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    private async Task<JsonElement> Put<T>(string url, object body)
    {
        var response = await _client.PutAsJsonAsync(url, body);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    private static List<string> Ids(JsonDocument doc) =>
        doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("placeId").GetString()!)
            .ToList();

    private static string VenuesMediaDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Nereye.Api", "wwwroot", "media", "venues");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("wwwroot/media/venues");
    }
}
