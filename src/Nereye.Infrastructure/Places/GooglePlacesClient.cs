using System.Net.Http.Json;
using System.Text.Json;
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

public sealed class GooglePlacesClient : IPlacesClient
{
    private static DateTimeOffset _quotaUntil;
    private readonly HttpClient _http;
    private readonly GooglePlacesOptions _options;

    public GooglePlacesClient(HttpClient http, IOptions<GooglePlacesOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress ??= new Uri("https://places.googleapis.com/");
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || !QuotaOpen())
        {
            return [];
        }

        var types = IntentPlaceTypes.GoogleTypes(intent);
        var radius = Math.Min(SearchRadius.Clamp(radiusMeters), 50_000);
        var body = new
        {
            includedTypes = types,
            maxResultCount = 20,
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = (double)radius
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchNearby")
        {
            Content = JsonContent.Create(body)
        };
        ApplyHeaders(request, "places.id,places.displayName,places.formattedAddress,places.location,places.types,places.photos.name,places.nationalPhoneNumber,places.websiteUri,places.rating,places.userRatingCount,places.googleMapsUri,places.currentOpeningHours,places.regularOpeningHours");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, cancellationToken))
        {
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ParseList(json);
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/places/{placeId}");
        ApplyHeaders(request, "id,displayName,formattedAddress,location,types,photos,photos.name,googleMapsUri,regularOpeningHours,regularOpeningHours.openNow,currentOpeningHours,currentOpeningHours.openNow");
        if (!QuotaOpen())
        {
            return null;
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, cancellationToken))
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ParsePlace(json);
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (!QuotaOpen() || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return [];
        }

        var body = new
        {
            textQuery = query,
            pageSize = 10,
            locationBias = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = 3000.0
                }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchText")
        {
            Content = JsonContent.Create(body)
        };
        ApplyHeaders(request, "places.id,places.displayName,places.formattedAddress,places.location,places.types,places.photos,places.photos.name,places.currentOpeningHours.openNow,places.regularOpeningHours.openNow");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!await Allow(response, cancellationToken))
        {
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ParseList(json);
    }

    public async Task<string?> FindPhotoAsync(
        string name,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (!QuotaOpen() || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(name))
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

    private static bool QuotaOpen() => DateTimeOffset.UtcNow >= _quotaUntil;

    private static async Task<bool> Allow(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if ((int)response.StatusCode == 429)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Contains("quota", StringComparison.OrdinalIgnoreCase)
                || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || body.Length == 0)
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                var pacific = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                var next = pacific.Date.AddDays(1);
                _quotaUntil = new DateTimeOffset(next, tz.GetUtcOffset(next));
            }
        }

        return false;
    }

    private void ApplyHeaders(HttpRequestMessage request, string fieldMask)
    {
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
    }

    private static IReadOnlyList<PlaceSnapshot> ParseList(JsonElement json)
    {
        if (!json.TryGetProperty("places", out var places))
        {
            return [];
        }

        return places.EnumerateArray().Select(ParsePlace).Where(p => p is not null).Cast<PlaceSnapshot>().ToList();
    }

    private static PlaceSnapshot? ParsePlace(JsonElement p)
    {
        var id = p.TryGetProperty("id", out var idEl) ? idEl.GetString() : p.GetProperty("name").GetString()?.Replace("places/", "");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var name = p.TryGetProperty("displayName", out var dn) && dn.TryGetProperty("text", out var text)
            ? text.GetString() ?? id
            : id;
        var address = p.TryGetProperty("formattedAddress", out var addr) ? addr.GetString() : null;
        var lat = 0d;
        var lng = 0d;
        if (p.TryGetProperty("location", out var loc))
        {
            lat = loc.GetProperty("latitude").GetDouble();
            lng = loc.GetProperty("longitude").GetDouble();
        }

        var types = p.TryGetProperty("types", out var typesEl)
            ? typesEl.EnumerateArray().Select(t => t.GetString() ?? "").Where(s => s.Length > 0).ToList()
            : [];

        string? photo = null;
        if (p.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
        {
            photo = photos[0].TryGetProperty("name", out var pn) ? pn.GetString() : null;
        }

        var maps = p.TryGetProperty("googleMapsUri", out var gmu) ? gmu.GetString() : null;
        bool? openNow = ReadOpenNow(p);
        var phone = p.TryGetProperty("nationalPhoneNumber", out var phoneEl) ? phoneEl.GetString() : null;
        var website = p.TryGetProperty("websiteUri", out var webEl) ? webEl.GetString() : null;
        double? rating = p.TryGetProperty("rating", out var ratingEl) && ratingEl.ValueKind == JsonValueKind.Number
            ? ratingEl.GetDouble()
            : null;
        int? ratingCount = p.TryGetProperty("userRatingCount", out var countEl) && countEl.ValueKind == JsonValueKind.Number
            ? countEl.GetInt32()
            : null;
        var hours = WeekdayText(p);

        return new PlaceSnapshot(
            id,
            name,
            address,
            lat,
            lng,
            types,
            openNow,
            photo,
            rating,
            ratingCount,
            maps,
            [],
            "Harita verisi: Google",
            website,
            null,
            phone,
            hours);
    }

    private static string? WeekdayText(JsonElement p)
    {
        if (!p.TryGetProperty("regularOpeningHours", out var hours) || !hours.TryGetProperty("weekdayDescriptions", out var days))
        {
            return null;
        }

        var lines = days.EnumerateArray().Select(d => d.GetString()).Where(s => !string.IsNullOrWhiteSpace(s));
        var text = string.Join("; ", lines);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool? ReadOpenNow(JsonElement p)
    {
        if (p.TryGetProperty("currentOpeningHours", out var current) && current.TryGetProperty("openNow", out var currentOpen))
        {
            return currentOpen.GetBoolean();
        }

        if (p.TryGetProperty("regularOpeningHours", out var hours) && hours.TryGetProperty("openNow", out var on))
        {
            return on.GetBoolean();
        }

        return null;
    }
}
