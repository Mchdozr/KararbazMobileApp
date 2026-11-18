using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nereye.Application.Common;

namespace Nereye.Api.Tests;

/// <summary>Elle ilerletilen saat: yeniden kullanım penceresinin dışına çıkmak için.</summary>
public sealed class TestClock : IClock
{
    private long _offsetTicks;

    public DateTime UtcNow => DateTime.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);
}

public sealed class ClockApiFactory : NereyeApiFactory
{
    public TestClock Clock { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(Clock);
    }
}

public sealed class SessionSecurityTests : IClassFixture<ClockApiFactory>
{
    private readonly ClockApiFactory _factory;

    public SessionSecurityTests(ClockApiFactory factory) => _factory = factory;

    private async Task<(string Email, string Refresh)> Signup(HttpClient client)
    {
        var email = $"s{Guid.NewGuid():N}@nereye.test";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { acceptTerms = true, email, password = "kahve1234", displayName = "Oturum" });
        res.EnsureSuccessStatusCode();
        return (email, JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("refreshToken").GetString()!);
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });

    private static async Task<string> NewRefresh(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("refreshToken").GetString()!;

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_closes_every_session_of_the_user()
    {
        using var client = _factory.CreateClient();
        var (email, first) = await Signup(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" });
        var otherDevice = await NewRefresh(login);

        var rotated = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = await NewRefresh(rotated);

        // Saldırgan çaldığı eski token'ı pencere dışında kullanıyor.
        _factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var reuse = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Contains("oturumların kapatıldı", await reuse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, otherDevice)).StatusCode);
        // Şifreyle yeniden giriş çalışır.
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Two_concurrent_refreshes_leave_exactly_one_valid_token()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await Signup(client);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Refresh(client, token)));
        var winners = results.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        Assert.Single(winners);
        Assert.All(results.Except(winners), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));

        // Kazananın token'ı hâlâ geçerli: eşzamanlı istek hırsızlık sayılıp herkesi düşürmedi.
        var next = await Refresh(client, await NewRefresh(winners[0]));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task A_logged_out_token_cannot_refresh()
    {
        using var client = _factory.CreateClient();
        var (email, _) = await Signup(client);
        var login = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "kahve1234" })).Content.ReadAsStringAsync()).RootElement;
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var refresh = login.GetProperty("refreshToken").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refresh })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, refresh)).StatusCode);
    }
}
