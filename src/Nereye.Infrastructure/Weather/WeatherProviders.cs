using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Application.Weather;
using Nereye.Domain.Geo;
using Nereye.Domain.Weather;

namespace Nereye.Infrastructure.Weather;

/// <summary>
/// Hava ayarları (<c>Weather</c>). Open-Meteo'nun ücretsiz uç noktası ticari olmayan kullanım içindir; yayında
/// açmak (Enabled) ya da ücretli uç noktayı (BaseUrl + ApiKey) vermek insan kararıdır (docs/hava-durumu.md).
/// </summary>
public sealed class WeatherOptions
{
    public const string Section = "Weather";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
    public string? ApiKey { get; set; }

    /// <summary>Sahte veri modunda hava: clear, cloudy, rain, snow, storm, fog.</summary>
    public string Fake { get; set; } = "clear";
}

public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);
    private readonly HttpClient _http;
    private readonly IPlacesCache _cache;
    private readonly WeatherOptions _options;

    public OpenMeteoWeatherProvider(HttpClient http, IPlacesCache cache, IOptions<WeatherOptions> options)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _http.Timeout = TimeSpan.FromSeconds(4);
    }

    public async Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        // ~5 km'lik bölge başına 20 dk: bir semtte tek istek.
        var area = Geohash.Encode(latitude, longitude, 5);
        var key = $"weather:v1:{area}";
        var hit = await _cache.TryGetAsync<WeatherNow>(key, cancellationToken);
        if (hit is not null)
        {
            return hit;
        }

        var (lat, lng) = Geohash.Decode(area);
        var baseUrl = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{baseUrl}v1/forecast?latitude={lat:F3}&longitude={lng:F3}&current=temperature_2m,weather_code,is_day&timezone=Europe%2FIstanbul");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            url += "&apikey=" + Uri.EscapeDataString(_options.ApiKey);
        }

        try
        {
            using var res = await _http.GetAsync(url, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var current = json.GetProperty("current");
            var now = new WeatherNow(
                WeatherRules.FromWmo(current.GetProperty("weather_code").GetInt32()),
                current.GetProperty("temperature_2m").GetDouble(),
                current.GetProperty("is_day").GetInt32() == 1);
            await _cache.SetAsync(key, Ttl, now, cancellationToken);
            return now;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}

public sealed class FakeWeatherProvider : IWeatherProvider
{
    private readonly WeatherKind _kind;

    public FakeWeatherProvider(IOptions<WeatherOptions> options) =>
        _kind = Enum.TryParse<WeatherKind>(options.Value.Fake, ignoreCase: true, out var k) ? k : WeatherKind.Clear;

    public Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        // Gündüz/gece İstanbul saatine göre: gece "hava güzel, açık havaya çık" denmesin.
        var hour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Domain.Places.OpeningSchedule.Zone).Hour;
        var temperature = _kind is WeatherKind.Snow ? -1 : _kind is WeatherKind.Rain or WeatherKind.Storm ? 12 : 22;
        return Task.FromResult<WeatherNow?>(new WeatherNow(_kind, temperature, hour is >= 7 and < 19));
    }
}

public sealed class NoWeatherProvider : IWeatherProvider
{
    public Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<WeatherNow?>(null);
}
