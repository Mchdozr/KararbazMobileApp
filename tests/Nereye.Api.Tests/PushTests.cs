using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nereye.Application.Push;
using Nereye.Infrastructure.Push;

namespace Nereye.Api.Tests;

public sealed class CapturingPush : IPushSender
{
    public List<PushMessage> Sent { get; } = [];
    public HashSet<string> Dead { get; } = [];

    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken = default)
    {
        lock (Sent)
        {
            Sent.AddRange(messages);
        }

        return Task.FromResult<IReadOnlyList<PushResult>>(messages.Select(m => new PushResult(m.To, !Dead.Contains(m.To), Dead.Contains(m.To))).ToList());
    }
}

public sealed class PushApiFactory : NereyeApiFactory
{
    public CapturingPush Push { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IPushSender>();
        services.AddSingleton<IPushSender>(Push);
    }
}

public sealed class PushTests : IClassFixture<PushApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PushApiFactory _factory;

    public PushTests(PushApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string Token)> UserAsync(string name = "Bildirim")
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new { acceptTerms = true, email = $"push{Guid.NewGuid():N}@nereye.test", password = "Passw0rd!", displayName = name });
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private static string Token() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    [Fact]
    public async Task Registering_a_device_requires_a_valid_token_and_records_consent()
    {
        var (client, _) = await UserAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/me/push", new { token = "raw", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/me/push", new { token = Token(), platform = "android" })).StatusCode);
        var consents = await client.GetFromJsonAsync<Dictionary<string, bool>>("/api/me/consents", Json);
        Assert.True(consents!["notifications"]);

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/me/push", new { token = Token(), platform = "android" })).StatusCode);
    }

    [Fact]
    public async Task Favorited_venue_event_tonight_is_reminded_once_and_dead_tokens_are_dropped()
    {
        // Mekan sahibi bu akşam için etkinlik ekler.
        var (owner, _) = await UserAsync("Sahip");
        async Task<JsonElement> Post(HttpClient c, string url, object body)
        {
            var r = await c.PostAsJsonAsync(url, body);
            var raw = await r.Content.ReadAsStringAsync();
            Assert.True(r.IsSuccessStatusCode, raw);
            return raw.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(raw, Json);
        }

        var claim = await Post(owner, "/api/venues/nereye:federica/claims", new { message = "Sahibiyim" });
        using var adminClient = _factory.CreateClient();
        var admin = await Post(adminClient, "/api/auth/login", new { email = "admin@nereye.app", password = "Admin!12345" });
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.GetProperty("accessToken").GetString());
        (await adminClient.PostAsync($"/api/admin/claims/{claim.GetProperty("id").GetGuid()}/approve", null)).EnsureSuccessStatusCode();
        var ownerEmail = (await owner.GetFromJsonAsync<JsonElement>("/api/me", Json)).GetProperty("email").GetString();
        var relogin = await Post(_factory.CreateClient(), "/api/auth/login", new { email = ownerEmail, password = "Passw0rd!" });
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", relogin.GetProperty("accessToken").GetString());
        var startsAt = DateTime.UtcNow.AddMinutes(90);
        await Post(owner, "/api/venues/nereye:federica/events", new { title = "Tadım gecesi", startsAt, endsAt = startsAt.AddHours(2) });

        // Bir kullanıcı mekanı kaydetmiş ve iki cihaz kaydetmiş; biri artık geçersiz.
        var (fan, _) = await UserAsync("Hayran");
        (await fan.PostAsync("/api/venues/nereye:federica/favorite", null)).EnsureSuccessStatusCode();
        var live = Token();
        var dead = Token();
        _factory.Push.Dead.Add(dead);
        await fan.PostAsJsonAsync("/api/me/push", new { token = live, platform = "ios" });
        await fan.PostAsJsonAsync("/api/me/push", new { token = dead, platform = "android" });

        // Mekanı kaydetmemiş kullanıcı bildirim almaz.
        var (other, _) = await UserAsync("Diğer");
        var otherToken = Token();
        await other.PostAsJsonAsync("/api/me/push", new { token = otherToken, platform = "ios" });

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<EventReminderJob>();
        var sent = await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(2, sent);
        var msg = Assert.Single(_factory.Push.Sent, m => m.To == live);
        Assert.Equal("Bu akşam: Tadım gecesi", msg.Title);
        Assert.Contains("başlıyor", msg.Body);
        Assert.Equal("nereyego://venue/nereye:federica", msg.Url);
        Assert.DoesNotContain(_factory.Push.Sent, m => m.To == otherToken);

        // İkinci tur aynı etkinlik için tekrar göndermez; geçersiz belirteç silindi.
        _factory.Push.Sent.Clear();
        Assert.Equal(0, await job.RunOnceAsync(CancellationToken.None));
        Assert.Empty(_factory.Push.Sent);
        Assert.Equal(HttpStatusCode.NoContent, (await fan.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me/push") { Content = JsonContent.Create(new { token = live }) })).StatusCode);
    }
}

public sealed class ExpoPushSenderTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public List<(Uri Uri, string Body, string? Auth)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.RequestUri!, body, request.Headers.Authorization?.ToString()));
            var count = JsonDocument.Parse(body).RootElement.GetArrayLength();
            var data = string.Join(",", Enumerable.Range(0, count).Select(i => i == 1
                ? """{"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}}"""
                : """{"status":"ok","id":"x"}"""));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{"data":[{{data}}]}""", System.Text.Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Sends_in_chunks_and_reports_unregistered_devices()
    {
        var handler = new Handler();
        var sender = new ExpoPushSender(new HttpClient(handler), Options.Create(new PushOptions { Enabled = true, AccessToken = "erisim" }), NullLogger<ExpoPushSender>.Instance);
        var messages = Enumerable.Range(0, 150).Select(i => new PushMessage($"ExponentPushToken[t{i}]", "Başlık", "Metin", "nereyego://venue/x")).ToList();

        var results = await sender.SendAsync(messages);

        Assert.Equal(2, handler.Calls.Count); // 100 + 50
        Assert.Equal("exp.host", handler.Calls[0].Uri.Host);
        Assert.Equal("Bearer erisim", handler.Calls[0].Auth);
        Assert.Contains("\"url\":\"nereyego://venue/x\"", handler.Calls[0].Body);
        Assert.Equal(150, results.Count);
        Assert.True(results[1].Unregistered);
        Assert.True(results[101].Unregistered);
        Assert.True(results[0].Ok);
    }
}
