using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Infrastructure.Places;
using Nereye.Application.Weather;
using Nereye.Domain.Geo;
using Nereye.Domain.Weather;

namespace Nereye.Infrastructure.Weather;

/// <summary>
/// Hava ayarları (<c>Weather</c>). Varsayılan sağlayıcı MET Norway: ücretsiz, ticari kullanım serbest (CC BY 4.0,
/// atıf gerekir). Open-Meteo yalnızca ücretli abonelikle (BaseUrl + ApiKey) seçilmeli; ücretsiz ucu ticari değildir
/// (docs/hava-durumu.md).
/// </summary>
public sealed class WeatherOptions
{
    public const string Section = "Weather";
    public bool Enabled { get; set; }

    /// <summary>"MetNorway" (varsayılan) ya da "OpenMeteo".</summary>
    public string Provider { get; set; } = "MetNorway";

    public string MetBaseUrl { get; set; } = "https://api.met.no/";

    /// <summary>MET'e giden User-Agent. Boşsa uygulama kimliği (HttpIdentity: "Nereye/1.0 (Places:Contact)").</summary>
    public string? UserAgent { get; set; }

    /// <summary>Open-Meteo uç noktası.</summary>
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
    public string? ApiKey { get; set; }

    public bool UsesOpenMeteo => string.Equals(Provider, "OpenMeteo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Sahte veri modunda hava: clear, cloudy, rain, snow, storm, fog; "live" gerçek sağlayıcıyı kullanır.</summary>
    public string Fake { get; set; } = "clear";

    public bool FakeIsLive => string.Equals(Fake, "live", StringComparison.OrdinalIgnoreCase);
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
                current.GetProperty("is_day").GetInt32() == 1,
                "Open-Meteo");
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

/// <summary>MET'ten alınan seri ve yeniden sorma zamanı. Önbellekte değişmez kayıt olarak durur.</summary>
public sealed record MetForecastEntry(IReadOnlyList<MetHour> Hours, string? LastModified, DateTime FreshUntil);

/// <summary>
/// MET Norway Locationforecast 2.0 (/compact). Kurallar (api.met.no/doc/TermsOfService):
/// <list type="bullet">
/// <item>Tanımlayıcı User-Agent; koordinat en çok 4 ondalık (burada ~5 km bölge merkezi, kullanıcının tam konumu gitmez).</item>
/// <item>Aynı istek Expires'tan önce ve Kuzey Avrupa dışı için 3 saatten sık tekrarlanmaz; sonra If-Modified-Since.</item>
/// <item>gzip (birincil işleyicide açık) ve yönlendirme desteği.</item>
/// </list>
/// Tahmin serisi saklanır, "şu an" her istekte seriden okunur. Hata olursa eski seri kullanılır ve bir süre sorulmaz.
/// Tekil kayıtlıdır: aynı bölge için eşzamanlı istekler tek çağrıya iner.
/// </summary>
public sealed class MetNorwayWeatherProvider : IWeatherProvider
{
    public const string Source = "MET Norway";
    public const string HttpClientName = "met-norway";

    internal static readonly TimeSpan MinInterval = TimeSpan.FromHours(3);
    internal static readonly TimeSpan MaxFresh = TimeSpan.FromHours(12);
    private static readonly TimeSpan Keep = TimeSpan.FromHours(30);
    private static readonly TimeSpan RetryAfterError = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryAfterRefusal = TimeSpan.FromHours(1);

    private readonly Func<HttpClient> _client;
    private readonly IPlacesCache _cache;
    private readonly IClock _clock;
    private readonly WeatherOptions _options;
    private readonly ILogger<MetNorwayWeatherProvider>? _log;
    private readonly ConcurrentDictionary<string, Task<MetForecastEntry?>> _inFlight = new();
    private int _deprecationLogged;

    public MetNorwayWeatherProvider(
        Func<HttpClient> client,
        IPlacesCache cache,
        IClock clock,
        WeatherOptions options,
        ILogger<MetNorwayWeatherProvider>? log = null)
    {
        _client = client;
        _cache = cache;
        _clock = clock;
        _options = options;
        _log = log;
    }

    public async Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var area = Geohash.Encode(latitude, longitude, 5);
        var key = $"weather:v2:met:{area}";
        var now = _clock.UtcNow;
        var entry = await _cache.TryGetAsync<MetForecastEntry>(key, cancellationToken);
        if (entry is null || now >= entry.FreshUntil)
        {
            entry = await SharedRefresh(key, area, entry).WaitAsync(cancellationToken);
        }

        var hour = entry is null ? null : MetForecastJson.At(entry.Hours, _clock.UtcNow);
        if (hour is null)
        {
            return null;
        }

        var (lat, lng) = Geohash.Decode(area);
        return new WeatherNow(WeatherRules.FromMetSymbol(hour.Symbol), hour.TemperatureC, SunPosition.IsDay(lat, lng, _clock.UtcNow), Source);
    }

    /// <summary>
    /// Aynı bölge için süren çağrıya katılır ya da yenisini başlatır. Çağrı tek bir isteğin iptaline bağlanmaz
    /// (HttpClient zaman aşımı sınırlar) ve asla hata fırlatmaz: sorun olursa eski kayıt döner.
    /// </summary>
    private Task<MetForecastEntry?> SharedRefresh(string key, string area, MetForecastEntry? old)
    {
        var mine = new TaskCompletionSource<MetForecastEntry?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = _inFlight.GetOrAdd(key, mine.Task);
        if (running != mine.Task)
        {
            return running;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                mine.TrySetResult(await RefreshAsync(key, area, old));
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "MET hava tazelemesi durdu: {Area}", area);
                mine.TrySetResult(old);
            }
            finally
            {
                _inFlight.TryRemove(new KeyValuePair<string, Task<MetForecastEntry?>>(key, mine.Task));
            }
        });
        return mine.Task;
    }

    private async Task<MetForecastEntry?> RefreshAsync(string key, string area, MetForecastEntry? old)
    {
        var now = _clock.UtcNow;
        var (lat, lng) = Geohash.Decode(area);
        var baseUrl = _options.MetBaseUrl.EndsWith('/') ? _options.MetBaseUrl : _options.MetBaseUrl + "/";
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{baseUrl}weatherapi/locationforecast/2.0/compact?lat={Math.Round(lat, 4):0.####}&lon={Math.Round(lng, 4):0.####}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent());
        request.Headers.Accept.ParseAdd("application/json");
        // Seri saat dışında kaldıysa koşulsuz sor: 304 eski seriyi geri verirdi.
        var conditional = old is { Hours.Count: > 0 } && MetForecastJson.At(old.Hours, now) is not null;
        if (conditional && DateTimeOffset.TryParse(old!.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
        {
            request.Headers.IfModifiedSince = since;
        }

        MetForecastEntry next;
        try
        {
            using var res = await _client().SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
            if (res.StatusCode == HttpStatusCode.NotModified && old is not null)
            {
                next = old with { FreshUntil = FreshUntil(now, res.Content.Headers.Expires) };
            }
            else if (res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NonAuthoritativeInformation)
            {
                if (res.StatusCode == HttpStatusCode.NonAuthoritativeInformation && Interlocked.Exchange(ref _deprecationLogged, 1) == 0)
                {
                    _log?.LogWarning("MET Locationforecast 203: bu sürüm kullanımdan kalkıyor, yeni sürüme geçilmeli.");
                }

                var hours = MetForecastJson.Parse(await res.Content.ReadAsStringAsync());
                if (hours.Count == 0)
                {
                    return await FailAsync(key, old, now, RetryAfterError);
                }

                next = new MetForecastEntry(
                    hours,
                    res.Content.Headers.LastModified?.ToString("R", CultureInfo.InvariantCulture),
                    FreshUntil(now, res.Content.Headers.Expires));
            }
            else
            {
                _log?.LogWarning("MET hava isteği {Status} döndü: {Area}", (int)res.StatusCode, area);
                var refused = res.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden;
                return await FailAsync(key, old, now, refused ? RetryAfterRefusal : RetryAfterError);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _log?.LogWarning(ex, "MET hava isteği başarısız: {Area}", area);
            return await FailAsync(key, old, now, RetryAfterError);
        }

        await _cache.SetAsync(key, Keep, next, CancellationToken.None);
        return next;
    }

    /// <summary>Eski seri (varsa) korunur; bölge bir süre sorulmaz.</summary>
    private async Task<MetForecastEntry?> FailAsync(string key, MetForecastEntry? old, DateTime now, TimeSpan retry)
    {
        var next = (old ?? new MetForecastEntry([], null, now)) with { FreshUntil = now + retry };
        await _cache.SetAsync(key, old is null ? retry : Keep, next, CancellationToken.None);
        return next;
    }

    /// <summary>Expires'a uyulur, ama 3 saatten erken ve 12 saatten geç değil.</summary>
    internal static DateTime FreshUntil(DateTime now, DateTimeOffset? expires)
    {
        var at = expires?.UtcDateTime ?? now;
        if (at < now + MinInterval)
        {
            at = now + MinInterval;
        }

        return at > now + MaxFresh ? now + MaxFresh : at;
    }

    private string UserAgent()
    {
        var ua = _options.UserAgent?.Trim();
        return string.IsNullOrEmpty(ua) || ua.Any(char.IsControl) ? HttpIdentity.UserAgent : ua;
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
        return Task.FromResult<WeatherNow?>(new WeatherNow(_kind, temperature, hour is >= 7 and < 19, "demo"));
    }
}

public sealed class NoWeatherProvider : IWeatherProvider
{
    public Task<WeatherNow?> CurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<WeatherNow?>(null);
}
