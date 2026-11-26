using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nereye.Application.Weather;
using Nereye.Domain.Weather;
using Nereye.Infrastructure.Places;
using Nereye.Infrastructure.Weather;

namespace Nereye.Api.Tests;

public sealed class SwitchableWeather : IWeatherProvider
{
    public WeatherNow? Now { get; set; }
    public TimeSpan Delay { get; set; }

    public async Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        return Now;
    }
}

public sealed class WeatherApiFactory : NereyeApiFactory
{
    public SwitchableWeather Weather { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IWeatherProvider>();
        services.AddSingleton<IWeatherProvider>(Weather);
    }
}

public sealed class WeatherTests : IClassFixture<WeatherApiFactory>
{
    private readonly WeatherApiFactory _factory;

    public WeatherTests(WeatherApiFactory factory) => _factory = factory;

    private async Task<JsonElement> RecommendAsync(string intent)
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/recommendations?lat=40.9885&lng=29.0250&intent={intent}&radiusMeters=5000");
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static double ScoreOf(JsonElement doc, string placeId) =>
        doc.GetProperty("items").EnumerateArray().First(i => i.GetProperty("placeId").GetString() == placeId).GetProperty("score").GetDouble();

    [Fact]
    public async Task Rain_lowers_outdoor_score_and_explains_it()
    {
        _factory.Weather.Delay = TimeSpan.Zero;
        _factory.Weather.Now = new WeatherNow(WeatherKind.Clear, 22, true);
        var sunny = await RecommendAsync("AcikHava");
        _factory.Weather.Now = new WeatherNow(WeatherKind.Rain, 12, true);
        var rainy = await RecommendAsync("AcikHava");

        Assert.True(ScoreOf(rainy, "nereye:moda") < ScoreOf(sunny, "nereye:moda"));
        var weather = rainy.GetProperty("weather");
        Assert.Equal("Rain", weather.GetProperty("kind").GetString());
        Assert.Contains("Yağmur", weather.GetProperty("note").GetString());
        Assert.Equal("Kultur", weather.GetProperty("suggestIntent").GetString());
    }

    [Fact]
    public async Task Slow_weather_never_slows_the_list()
    {
        _factory.Weather.Now = new WeatherNow(WeatherKind.Rain, 12, true);
        _factory.Weather.Delay = TimeSpan.FromSeconds(5);
        var started = DateTime.UtcNow;
        var doc = await RecommendAsync("Kahve");
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2.5));
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("weather").ValueKind);
        _factory.Weather.Delay = TimeSpan.Zero;
    }
}

public sealed class OpenMeteoTests
{
    private const string Body = """
        {"latitude":40.99,"longitude":29.02,"current":{"time":"2026-11-24T18:00","temperature_2m":13.4,"weather_code":63,"is_day":0}}
        """;

    private sealed class Handler : HttpMessageHandler
    {
        public List<Uri> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Parses_current_weather_and_caches_per_area()
    {
        var handler = new Handler();
        var cache = new DistributedPlacesCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var client = new OpenMeteoWeatherProvider(new HttpClient(handler), cache, Options.Create(new WeatherOptions { Enabled = true }));

        var now = await client.CurrentAsync(40.9885, 29.0250);
        Assert.Equal(new WeatherNow(WeatherKind.Rain, 13.4, false, "Open-Meteo"), now);
        Assert.Contains("current=temperature_2m,weather_code,is_day", handler.Calls[0].Query);
        Assert.Equal("api.open-meteo.com", handler.Calls[0].Host);

        // Yakın nokta aynı bölge: ikinci çağrı yok.
        await client.CurrentAsync(40.9890, 29.0260);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Paid_endpoint_and_key_come_from_configuration()
    {
        var handler = new Handler();
        var cache = new DistributedPlacesCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var client = new OpenMeteoWeatherProvider(new HttpClient(handler), cache,
            Options.Create(new WeatherOptions { Enabled = true, BaseUrl = "https://customer-api.open-meteo.com/", ApiKey = "test-anahtari" }));
        await client.CurrentAsync(41.04, 29.0);
        Assert.Equal("customer-api.open-meteo.com", handler.Calls[0].Host);
        Assert.Contains("apikey=test-anahtari", handler.Calls[0].Query);
    }
}

public sealed class MetNorwayTests
{
    // Kısaltılmış gerçek /compact yanıtı: 15:00 hafif yağmur, 16:00 açık.
    private const string Body = """
        {"type":"Feature","properties":{"meta":{"updated_at":"2026-11-24T14:10:00Z"},"timeseries":[
          {"time":"2026-11-24T15:00:00Z","data":{"instant":{"details":{"air_temperature":11.6}},"next_1_hours":{"summary":{"symbol_code":"lightrain"},"details":{}}}},
          {"time":"2026-11-24T16:00:00Z","data":{"instant":{"details":{"air_temperature":10.9}},"next_1_hours":{"summary":{"symbol_code":"clearsky_night"},"details":{}}}},
          {"time":"2026-11-24T17:00:00Z","data":{"instant":{"details":{"air_temperature":10.1}},"next_1_hours":{"summary":{"symbol_code":"cloudy"},"details":{}}}},
          {"time":"2026-11-24T18:00:00Z","data":{"instant":{"details":{"air_temperature":9.8}},"next_1_hours":{"summary":{"symbol_code":"cloudy"},"details":{}}}},
          {"time":"2026-11-24T19:00:00Z","data":{"instant":{"details":{"air_temperature":9.4}},"next_1_hours":{"summary":{"symbol_code":"cloudy"},"details":{}}}}
        ]}}
        """;

    private const string LastModified = "Tue, 24 Nov 2026 14:12:00 GMT";

    private sealed class Handler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Calls { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public TimeSpan Delay { get; set; }
        public Func<DateTime>? Expires { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add(request);
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            var res = new HttpResponseMessage(Status);
            if (Status is HttpStatusCode.OK or HttpStatusCode.NonAuthoritativeInformation)
            {
                res.Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json");
                res.Content.Headers.LastModified = DateTimeOffset.Parse(LastModified, System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                res.Content = new StringContent("");
            }

            if (Expires is not null)
            {
                res.Content.Headers.Expires = Expires();
            }

            return res;
        }
    }

    private static (MetNorwayWeatherProvider Provider, Handler Handler, ManualClock Clock) Create(WeatherOptions? options = null)
    {
        var handler = new Handler();
        var clock = new ManualClock { UtcNow = new DateTime(2026, 11, 24, 15, 20, 0, DateTimeKind.Utc) };
        handler.Expires = () => clock.UtcNow.AddMinutes(30);
        var cache = new DistributedPlacesCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var provider = new MetNorwayWeatherProvider(() => new HttpClient(handler), cache, clock, options ?? new WeatherOptions { Enabled = true });
        return (provider, handler, clock);
    }

    [Fact]
    public async Task Reads_the_current_hour_with_identifying_request()
    {
        var (provider, handler, _) = Create();
        var now = await provider.CurrentAsync(40.9885, 29.0250);

        // 15:20 UTC = 18:20 İstanbul, kasımda gece.
        Assert.Equal(new WeatherNow(WeatherKind.Rain, 11.6, false, "MET Norway"), now);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("api.met.no", call.RequestUri!.Host);
        Assert.Equal("/weatherapi/locationforecast/2.0/compact", call.RequestUri.AbsolutePath);
        // Koordinat en çok 4 ondalık (MET kuralı) ve ~5 km bölge merkezi: kullanıcının tam konumu gitmez.
        Assert.Matches(@"^\?lat=\d+(\.\d{1,4})?&lon=\d+(\.\d{1,4})?$", call.RequestUri.Query);
        Assert.DoesNotContain("40.9885", call.RequestUri.Query);
        Assert.StartsWith("Nereye/", call.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task User_agent_can_be_set_explicitly()
    {
        var (provider, handler, _) = Create(new WeatherOptions { Enabled = true, UserAgent = "OrnekUygulama/2.0 destek.example" });
        await provider.CurrentAsync(41.04, 29.0);
        Assert.Equal("OrnekUygulama/2.0 destek.example", handler.Calls[0].Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Same_area_is_asked_at_most_every_three_hours_even_if_expires_is_sooner()
    {
        var (provider, handler, clock) = Create();
        await provider.CurrentAsync(40.9885, 29.0250);
        clock.Advance(TimeSpan.FromMinutes(40));
        var later = await provider.CurrentAsync(40.9890, 29.0260);

        Assert.Single(handler.Calls);
        // Önbellekteki seriden yeni saat okunur: 16:00 adımı.
        Assert.Equal(WeatherKind.Clear, later!.Kind);
        Assert.Equal(10.9, later.TemperatureC);
    }

    [Fact]
    public async Task After_three_hours_it_revalidates_with_if_modified_since()
    {
        var (provider, handler, clock) = Create();
        await provider.CurrentAsync(40.9885, 29.0250);
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
        handler.Status = HttpStatusCode.NotModified;
        var now = await provider.CurrentAsync(40.9885, 29.0250);

        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal(LastModified, handler.Calls[1].Headers.IfModifiedSince!.Value.ToString("R"));
        // 304: eski seri kullanılır (18:21 UTC adımı).
        Assert.Equal(9.8, now!.TemperatureC);

        // 304 tazeliği de uzattı: hemen yeni istek yok.
        await provider.CurrentAsync(40.9885, 29.0250);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Longer_expires_is_respected()
    {
        var (provider, handler, clock) = Create();
        handler.Expires = () => clock.UtcNow.AddHours(5);
        await provider.CurrentAsync(40.9885, 29.0250);
        clock.Advance(TimeSpan.FromHours(4));
        await provider.CurrentAsync(40.9885, 29.0250);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Failure_backs_off_and_keeps_the_old_forecast()
    {
        var (provider, handler, clock) = Create();
        await provider.CurrentAsync(40.9885, 29.0250);
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
        handler.Status = HttpStatusCode.InternalServerError;

        var now = await provider.CurrentAsync(40.9885, 29.0250);
        Assert.Equal(9.8, now!.TemperatureC);
        Assert.Equal(2, handler.Calls.Count);

        // Hemen tekrar sorulmaz.
        clock.Advance(TimeSpan.FromMinutes(5));
        await provider.CurrentAsync(40.9885, 29.0250);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Unknown_area_failure_returns_null_and_is_not_hammered()
    {
        var (provider, handler, clock) = Create();
        handler.Status = HttpStatusCode.TooManyRequests;
        Assert.Null(await provider.CurrentAsync(40.9885, 29.0250));
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Null(await provider.CurrentAsync(40.9885, 29.0250));
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Deprecated_version_status_still_works()
    {
        var (provider, handler, _) = Create();
        handler.Status = HttpStatusCode.NonAuthoritativeInformation;
        Assert.NotNull(await provider.CurrentAsync(40.9885, 29.0250));
    }

    [Fact]
    public async Task Concurrent_requests_for_one_area_share_one_call()
    {
        var (provider, handler, _) = Create();
        handler.Delay = TimeSpan.FromMilliseconds(200);
        var all = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => provider.CurrentAsync(40.9885, 29.0250)));
        Assert.All(all, w => Assert.Equal(WeatherKind.Rain, w!.Kind));
        Assert.Single(handler.Calls);
    }
}
