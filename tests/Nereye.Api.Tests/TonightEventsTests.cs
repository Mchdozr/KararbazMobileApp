using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nereye.Api.Tests;

/// <summary>Bu akşam etkinlikler: sahip ekler/siler, ana sayfa yakındakileri görür. Saat sabit: 18:00 İstanbul.</summary>
public sealed class TonightEventsTests : IClassFixture<ControlledApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ControlledApiFactory _factory;

    public TonightEventsTests(ControlledApiFactory factory)
    {
        _factory = factory;
        // Oturum belirteci gerçek saatle doğrulanır: bu sınıfta saat gerçek zamandan başlar.
        _factory.Clock.UtcNow = DateTime.UtcNow;
    }

    /// <summary>Bu akşam penceresinin içinde kalan bir başlangıç (gece 03:30'da çalışsa bile).</summary>
    private DateTime TonightStart()
    {
        var now = _factory.Clock.UtcNow;
        var (_, to) = Nereye.Domain.Events.EventRules.TonightWindow(now);
        return now + TimeSpan.FromTicks(Math.Min(TimeSpan.FromHours(1).Ticks, (to - now).Ticks / 2));
    }

    private async Task<(HttpClient Client, string Token)> OwnerOfAsync(string placeId)
    {
        var client = _factory.CreateClient();
        async Task<JsonElement> Post(string url, object body)
        {
            var response = await client.PostAsJsonAsync(url, body);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, raw);
            return JsonSerializer.Deserialize<JsonElement>(raw, Json);
        }

        var email = $"etkinlik{Guid.NewGuid():N}@nereye.test";
        var reg = await Post("/api/auth/register", new { acceptTerms = true, email, password = "Passw0rd!", displayName = "Sahne" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        var claim = await Post($"/api/venues/{placeId}/claims", new { message = "Sahibiyim" });
        var admin = await Post("/api/auth/login", new { email = "admin@nereye.app", password = "Admin!12345" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.GetProperty("accessToken").GetString());
        (await client.PostAsync($"/api/admin/claims/{claim.GetProperty("id").GetGuid()}/approve", null)).EnsureSuccessStatusCode();
        var login = await Post("/api/auth/login", new { email, password = "Passw0rd!" });
        var token = login.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    [Fact]
    public async Task Owner_adds_tonight_event_and_nearby_users_see_it()
    {
        var (owner, _) = await OwnerOfAsync("nereye:mini");
        var now = _factory.Clock.UtcNow;
        var tonight = await owner.PostAsJsonAsync("/api/venues/nereye:mini/events",
            new { title = "Akustik gece", startsAt = TonightStart(), endsAt = TonightStart().AddHours(2) });
        Assert.Equal(HttpStatusCode.OK, tonight.StatusCode);
        var created = await tonight.Content.ReadFromJsonAsync<JsonElement>(Json);
        await owner.PostAsJsonAsync("/api/venues/nereye:mini/events",
            new { title = "Hafta sonu konseri", startsAt = now.AddDays(3), endsAt = now.AddDays(3).AddHours(2) });

        using var anyone = _factory.CreateClient();
        var near = await anyone.GetFromJsonAsync<JsonElement>("/api/events/tonight?lat=40.9885&lng=29.0250&radiusMeters=5000", Json);
        var items = near.GetProperty("items").EnumerateArray().ToList();
        var mine = Assert.Single(items, i => i.GetProperty("title").GetString() == "Akustik gece");
        Assert.Equal("nereye:mini", mine.GetProperty("placeId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("venueName").GetString()));
        Assert.True(mine.GetProperty("distanceMeters").GetDouble() < 5000);
        Assert.False(mine.GetProperty("live").GetBoolean());
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == "Hafta sonu konseri");

        // Uzak semtte görünmez.
        var far = await anyone.GetFromJsonAsync<JsonElement>("/api/events/tonight?lat=41.1100&lng=29.0600&radiusMeters=1000", Json);
        Assert.DoesNotContain(far.GetProperty("items").EnumerateArray(), i => i.GetProperty("title").GetString() == "Akustik gece");

        // Sahip siler.
        var del = await owner.DeleteAsync($"/api/venues/nereye:mini/events/{created.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var after = await anyone.GetFromJsonAsync<JsonElement>("/api/events/tonight?lat=40.9885&lng=29.0250&radiusMeters=5000", Json);
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), i => i.GetProperty("title").GetString() == "Akustik gece");
    }

    [Fact]
    public async Task Only_the_owner_can_add_and_input_is_validated()
    {
        var (owner, _) = await OwnerOfAsync("nereye:modern");
        var now = _factory.Clock.UtcNow;
        var bad = await owner.PostAsJsonAsync("/api/venues/nereye:modern/events", new { title = "x", startsAt = now.AddHours(1), endsAt = now.AddHours(2) });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("3–80", await bad.Content.ReadAsStringAsync());

        var other = await owner.PostAsJsonAsync("/api/venues/nereye:petra/events", new { title = "Başkasının mekanı", startsAt = now.AddHours(1), endsAt = now.AddHours(2) });
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        using var anonymous = _factory.CreateClient();
        var anon = await anonymous.PostAsJsonAsync("/api/venues/nereye:modern/events", new { title = "Anonim", startsAt = now.AddHours(1) });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    [Fact]
    public async Task Invalid_location_is_rejected()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/events/tonight?lat=95&lng=29")).StatusCode);
    }
}
