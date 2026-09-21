using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
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
        var types = IntentPlaceTypes.GoogleTypes(intent);
        var body = new
        {
            includedTypes = types,
            maxResultCount = 20,
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = (double)radiusMeters
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchNearby")
        {
            Content = JsonContent.Create(body)
        };
        ApplyHeaders(request, "places.id,places.displayName,places.formattedAddress,places.location,places.types,places.photos,places.photos.name,places.currentOpeningHours.openNow,places.regularOpeningHours.openNow");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ParseList(json);
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/places/{placeId}");
        ApplyHeaders(request, "id,displayName,formattedAddress,location,types,photos,photos.name,googleMapsUri,regularOpeningHours,regularOpeningHours.openNow,currentOpeningHours,currentOpeningHours.openNow");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
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
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ParseList(json);
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

        return new PlaceSnapshot(id, name, address, lat, lng, types, openNow, photo, null, null, maps, [], "Harita verisi: Google");
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
