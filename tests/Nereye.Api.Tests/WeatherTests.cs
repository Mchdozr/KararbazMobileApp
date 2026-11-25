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
        Assert.Equal(new WeatherNow(WeatherKind.Rain, 13.4, false), now);
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
