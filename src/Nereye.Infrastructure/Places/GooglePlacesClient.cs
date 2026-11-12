using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class GooglePlacesOptions
{
    public const string Section = "Places";
    public string ApiKey { get; set; } = string.Empty;
    public bool UseFake { get; set; } = true;
}

/// <summary>Bir mekanı adı ve konumuyla bulup zengin içeriğini (özet, yorum, olanak, galeri) getirebilen kaynak.</summary>
public interface IRichPlaceSource
{
    Task<PlaceSnapshot?> FindRichAsync(PlaceSnapshot target, CancellationToken cancellationToken = default);
}

public sealed class GooglePlacesClient : IPlacesClient, IRichPlaceSource
{
    // Google kotayı uç bazında sayar (Nearby, Text Search, Details, Photo). Biri dolunca yalnızca o uç
    // Pasifik gece yarısına kadar susar; diğerleri çalışmaya devam eder.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> QuotaUntil = new();
    private readonly HttpClient _http;
    private readonly GooglePlacesOptions _options;
    private readonly ILogger<GooglePlacesClient>? _log;

    public GooglePlacesClient(HttpClient http, IOptions<GooglePlacesOptions> options, ILogger<GooglePlacesClient>? log = null)
    {
        _log = log;
        _http = http;
        _options = options.Value;
        _http.BaseAddress ??= new Uri("https://places.googleapis.com/");
        // Google yavaşlarsa istek asılı kalmasın; öneriler OSM ile devam eder.
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    private bool Ready(string metric) => !string.IsNullOrWhiteSpace(_options.ApiKey) && QuotaOpen(metric);

    /// <summary>Anahtar var ve bu uç için günlük kota açık mı? (Çağıranlar boşuna beklemesin diye.)</summary>
    public bool CanSearchNearby => Ready(Nearby);

    public bool CanEnrich => Ready(TextMetric);

    private const string Nearby = "nearby";
    private const string TextMetric = "text";
    private const string Details = "details";
    private const string Photo = "photo";

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(Nearby))
        {
            return [];
        }

        var body = new
        {
            includedTypes = IntentPlaceTypes.GoogleTypes(intent),
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR",
            rankPreference = "POPULARITY",
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = (double)Math.Min(SearchRadius.Clamp(radiusMeters), 50_000)
                }
            }
        };

        var json = await PostAsync("v1/places:searchNearby", body, GooglePlaceJson.ListMask, Nearby, cancellationToken);
        return json is null ? [] : GooglePlaceJson.ParseList(json.Value);
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        if (!Ready(Details) || string.IsNullOrWhiteSpace(placeId))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v1/places/{Uri.EscapeDataString(placeId)}?languageCode=tr&regionCode=TR");
        ApplyHeaders(request, GooglePlaceJson.DetailMask);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, Details, cancellationToken))
        {
            return null;
        }

        return GooglePlaceJson.Parse(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        SearchAsync(query, latitude, longitude, 3000, 10, GooglePlaceJson.ListMask, cancellationToken);

    public async Task<PlaceSnapshot?> FindRichAsync(PlaceSnapshot target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.Name))
        {
            return null;
        }

        var hits = await SearchAsync(
            target.Name,
            target.Latitude,
            target.Longitude,
            250,
            4,
            GooglePlaceJson.SearchRichMask,
            cancellationToken);
        return PlaceMerge.Pick(target, hits);
    }

    public async Task<string?> FindPhotoAsync(
        string name,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(TextMetric) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            var hits = await TextSearchAsync(name, latitude, longitude, cancellationToken);
            foreach (var hit in hits)
            {
                var distance = GeoMath.HaversineMeters(latitude, longitude, hit.Latitude, hit.Longitude);
                var photo = PlacePhoto.FirstTrusted([hit.PhotoReference]);
                if (distance <= 120 && photo is not null)
                {
                    return photo;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>Fotoğraf adını (places/…/photos/…) anahtarsız, doğrudan indirilebilir CDN adresine çevirir.</summary>
    public async Task<string?> PhotoUriAsync(string name, int maxWidthPx, CancellationToken cancellationToken = default)
    {
        if (!Ready(Photo))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v1/{name}/media?maxWidthPx={Math.Clamp(maxWidthPx, 120, 1600)}&skipHttpRedirect=true");
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _options.ApiKey);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, Photo, cancellationToken))
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var uri = json.TryGetProperty("photoUri", out var el) ? el.GetString() : null;
        return uri is not null && uri.StartsWith("https://", StringComparison.Ordinal) ? uri : null;
    }

    private async Task<IReadOnlyList<PlaceSnapshot>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double biasMeters,
        int pageSize,
        string mask,
        CancellationToken cancellationToken)
    {
        if (!Ready(TextMetric) || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var body = new
        {
            textQuery = query,
            pageSize,
            languageCode = "tr",
            regionCode = "TR",
            locationBias = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = biasMeters
                }
            }
        };
        var json = await PostAsync("v1/places:searchText", body, mask, TextMetric, cancellationToken);
        return json is null ? [] : GooglePlaceJson.ParseList(json.Value);
    }

    private async Task<JsonElement?> PostAsync(string path, object body, string mask, string metric, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        ApplyHeaders(request, mask);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, metric, cancellationToken))
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static bool QuotaOpen(string metric) =>
        !QuotaUntil.TryGetValue(metric, out var until) || DateTimeOffset.UtcNow >= until;

    private async Task<bool> Allow(HttpResponseMessage response, string metric, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // Sessizce boş dönmek yerine nedenini yaz: yanlış anahtar, kapalı API ya da hatalı alan maskesi burada görünür.
        _log?.LogWarning(
            "Google Places {Status} for {Path}: {Body}",
            (int)response.StatusCode,
            response.RequestMessage?.RequestUri?.AbsolutePath,
            body.Length > 300 ? body[..300] : body);
        if ((int)response.StatusCode == 429)
        {
            if (body.Contains("quota", StringComparison.OrdinalIgnoreCase)
                || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || body.Length == 0)
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                var pacific = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                var next = pacific.Date.AddDays(1);
                QuotaUntil[metric] = new DateTimeOffset(next, tz.GetUtcOffset(next));
            }
        }

        return false;
    }

    private void ApplyHeaders(HttpRequestMessage request, string fieldMask)
    {
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
    }
}
