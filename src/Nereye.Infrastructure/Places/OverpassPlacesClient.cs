using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class OverpassPlacesClient : IPlacesClient
{
    private const string Attribution = "Harita verisi: OpenStreetMap";
    private static readonly Uri[] Endpoints =
    [
        new("https://overpass-api.de/api/interpreter"),
        new("https://overpass.kumi.systems/api/interpreter")
    ];
    private readonly HttpClient _http;
    private readonly ILogger<OverpassPlacesClient> _log;

    public OverpassPlacesClient(HttpClient http, ILogger<OverpassPlacesClient> log)
    {
        _http = http;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(40);
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var radius = Math.Clamp(radiusMeters, 300, 8000);
        var filters = string.Join(
            "",
            IntentPlaceTypes.OsmTags(intent).Select(t =>
                $"nwr[\"{Escape(t.Key)}\"=\"{Escape(t.Value)}\"](around:{radius.ToString(CultureInfo.InvariantCulture)},{Fmt(latitude)},{Fmt(longitude)});"));
        var query = $"[out:json][timeout:18];({filters});out center 50;";
        var json = await PostAsync(query, cancellationToken);
        return ParseList(json, latitude, longitude).Take(40).ToList();
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        if (!TryParseOsmId(placeId, out var type, out var id))
        {
            return null;
        }

        var body = type == "way" || type == "relation"
            ? $"[out:json][timeout:10];{type}({id});out center;"
            : $"[out:json][timeout:10];node({id});out;";
        var json = await PostAsync(body, cancellationToken);
        return ParseList(json, 0, 0).FirstOrDefault();
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var q = Regex.Escape(query.Trim());
        if (q.Length < 2)
        {
            return [];
        }

        var data =
            $"[out:json][timeout:18];nwr[\"name\"~\"{q}\",i](around:4000,{Fmt(latitude)},{Fmt(longitude)});out center 30;";
        var json = await PostAsync(data, cancellationToken);
        return ParseList(json, latitude, longitude).Take(20).ToList();
    }

    private async Task<JsonElement> PostAsync(string query, CancellationToken cancellationToken)
    {
        Exception? last = null;
        foreach (var uri in Endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain")
                };
                request.Headers.TryAddWithoutValidation("User-Agent", "Nereye/1.0 (https://nereye.app)");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                using var response = await _http.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _log.LogWarning("Overpass {Uri} {Status}: {Body}", uri, (int)response.StatusCode, err);
                    continue;
                }

                return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                last = ex;
                _log.LogWarning(ex, "Overpass endpoint failed {Uri}", uri);
            }
        }

        throw last ?? new HttpRequestException("Overpass unavailable");
    }

    internal static IReadOnlyList<PlaceSnapshot> ParseList(JsonElement json, double originLat, double originLng)
    {
        if (!json.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<PlaceSnapshot>();
        foreach (var el in elements.EnumerateArray())
        {
            var parsed = ParseElement(el);
            if (parsed is not null)
            {
                items.Add(parsed);
            }
        }

        if (originLat == 0 && originLng == 0)
        {
            return items;
        }

        return items
            .OrderBy(p => Nereye.Domain.Geo.GeoMath.HaversineMeters(originLat, originLng, p.Latitude, p.Longitude))
            .ToList();
    }

    internal static PlaceSnapshot? ParseElement(JsonElement el)
    {
        if (!el.TryGetProperty("type", out var typeEl) || !el.TryGetProperty("id", out var idEl))
        {
            return null;
        }

        var osmType = typeEl.GetString() ?? "node";
        var id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString(CultureInfo.InvariantCulture) : idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        double lat = 0;
        double lng = 0;
        if (el.TryGetProperty("lat", out var latEl) && el.TryGetProperty("lon", out var lonEl))
        {
            lat = latEl.GetDouble();
            lng = lonEl.GetDouble();
        }
        else if (el.TryGetProperty("center", out var center))
        {
            lat = center.GetProperty("lat").GetDouble();
            lng = center.GetProperty("lon").GetDouble();
        }

        if (lat == 0 && lng == 0)
        {
            return null;
        }

        JsonElement tags = default;
        var hasTags = el.TryGetProperty("tags", out tags);
        var name = hasTags && tags.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var amenity = Tag(tags, hasTags, "amenity");
        var shop = Tag(tags, hasTags, "shop");
        var leisure = Tag(tags, hasTags, "leisure");
        var tourism = Tag(tags, hasTags, "tourism");
        var googleType = IntentPlaceTypes.GoogleTypeFromOsm(amenity, shop, leisure, tourism);
        var street = Tag(tags, hasTags, "addr:street") ?? Tag(tags, hasTags, "addr:suburb") ?? Tag(tags, hasTags, "addr:city");
        var photo = PlacePhoto.FromHttpOrCommons(Tag(tags, hasTags, "image"), Tag(tags, hasTags, "wikimedia_commons"));
        var website = Tag(tags, hasTags, "website") ?? Tag(tags, hasTags, "contact:website");
        if (website is not null && !PlacePhoto.IsHttp(website))
        {
            website = null;
        }

        var hoursText = Tag(tags, hasTags, "opening_hours");
        var hours = OsmOpeningHours.Parse(hoursText);
        var maps = $"https://www.openstreetmap.org/{osmType}/{id}";
        return new PlaceSnapshot(
            $"osm:{osmType}:{id}",
            name,
            street,
            lat,
            lng,
            [googleType],
            OsmOpeningHours.IsOpen(hoursText, DateTimeOffset.Now),
            photo,
            null,
            null,
            maps,
            hours,
            Attribution,
            website,
            Tag(tags, hasTags, "wikidata"),
            Tag(tags, hasTags, "phone") ?? Tag(tags, hasTags, "contact:phone"),
            hoursText);
    }

    private static string? Tag(JsonElement tags, bool hasTags, string key) =>
        hasTags && tags.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static bool TryParseOsmId(string placeId, out string type, out string id)
    {
        type = "";
        id = "";
        var parts = placeId.Split(':');
        if (parts.Length != 3 || !parts[0].Equals("osm", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        type = parts[1];
        id = parts[2];
        return type is "node" or "way" or "relation" && Regex.IsMatch(id, "^[0-9]+$");
    }

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
