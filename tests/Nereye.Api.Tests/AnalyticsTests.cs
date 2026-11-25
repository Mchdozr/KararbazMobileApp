using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nereye.Application.Analytics;

namespace Nereye.Api.Tests;

[Collection("api-flow")]
public sealed class AnalyticsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NereyeApiFactory _factory;

    public AnalyticsTests(NereyeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Events_are_counted_per_day_without_identity()
    {
        using var client = _factory.CreateClient();
        var place = "osm:node:" + Random.Shared.Next(1_000_000, 9_999_999);
        var res = await client.PostAsJsonAsync("/api/telemetry/events", new
        {
            events = new object[]
            {
                new { name = "venue_view", placeId = place },
                new { name = "venue_view", placeId = place },
                new { name = "directions", placeId = place },
                new { name = "list_view", intent = "Kahve" },
                new { name = "bilinmeyen_olay" }
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var stats = await analytics.CountsAsync(place, 1);
        Assert.Equal(2, stats.Views);
        Assert.Equal(1, stats.Directions);
    }

    [Fact]
    public async Task Batch_limits_are_enforced()
    {
        using var client = _factory.CreateClient();
        var many = Enumerable.Range(0, 51).Select(_ => new { name = "app_open" }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/telemetry/events", new { events = many })).StatusCode);
    }

    [Fact]
    public async Task Funnel_reports_steps_for_admin()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/telemetry/events", new
        {
            events = new object[] { new { name = "app_open" }, new { name = "list_view", intent = "Gece" }, new { name = "decide_open" } }
        });
        using var scope = _factory.Services.CreateScope();
        var funnel = await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().FunnelAsync(7);
        Assert.Equal("app_open", funnel.Steps[0].Name);
        Assert.True(funnel.Steps[0].Count >= 1);
        Assert.Contains(funnel.Intents, i => i.Key == "Gece");
        Assert.True(funnel.Others.GetValueOrDefault("decide_open") >= 1);
    }

    [Fact]
    public async Task Analytics_consent_is_a_known_purpose()
    {
        using var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            acceptTerms = true,
            email = $"riza{Guid.NewGuid():N}@nereye.test",
            password = "Passw0rd!",
            displayName = "Rıza"
        });
        var token = (await reg.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PostAsJsonAsync("/api/me/consent", new { purpose = "analytics", granted = true })).EnsureSuccessStatusCode();
        var consents = await client.GetFromJsonAsync<Dictionary<string, bool>>("/api/me/consents", Json);
        Assert.True(consents!["analytics"]);
    }

    [Fact]
    public async Task Owner_sees_views_directions_and_saves_of_own_venue_only()
    {
        using var client = _factory.CreateClient();
        const string venue = "nereye:mandabatmaz";
        async Task<JsonElement> Post(string url, object body)
        {
            var response = await client.PostAsJsonAsync(url, body);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, raw);
            return raw.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(raw, Json);
        }

        var email = $"sahip{Guid.NewGuid():N}@nereye.test";
        var owner = await Post("/api/auth/register", new { acceptTerms = true, email, password = "Passw0rd!", displayName = "Sahip" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.GetProperty("accessToken").GetString());
        var claim = await Post($"/api/venues/{venue}/claims", new { message = "İşletmeciyim" });
        // Kaydetme sayısı gerçek kayıtlardan gelir (rızadan bağımsız toplam).
        await client.PostAsync($"/api/venues/{venue}/favorite", null);

        var admin = await Post("/api/auth/login", new { email = "admin@nereye.app", password = "Admin!12345" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.GetProperty("accessToken").GetString());
        (await client.PostAsync($"/api/admin/claims/{claim.GetProperty("id").GetGuid()}/approve", null)).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;
        await Post("/api/telemetry/events", new
        {
            events = new object[] { new { name = "venue_view", placeId = venue }, new { name = "directions", placeId = venue } }
        });

        var relogin = await Post("/api/auth/login", new { email, password = "Passw0rd!" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", relogin.GetProperty("accessToken").GetString());
        var stats = await client.GetFromJsonAsync<JsonElement>($"/api/venues/{venue}/stats", Json);
        Assert.True(stats.GetProperty("views").GetInt64() >= 1);
        Assert.True(stats.GetProperty("directions").GetInt64() >= 1);
        Assert.True(stats.GetProperty("saves").GetInt64() >= 1);
        Assert.Equal(30, stats.GetProperty("days").GetArrayLength());

        var other = await client.GetAsync("/api/venues/nereye:petra/stats");
        Assert.True(other.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, other.StatusCode.ToString());
    }
}
