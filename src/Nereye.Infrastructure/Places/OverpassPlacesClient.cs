using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class OverpassPlacesClient : IPlacesClient
{
    private const string Attribution = "Harita verisi: OpenStreetMap";
    private readonly Uri[] _endpoints;
    private readonly HttpClient _http;
    private readonly ILogger<OverpassPlacesClient> _log;

    public OverpassPlacesClient(HttpClient http, ILogger<OverpassPlacesClient> log, IOptions<OverpassOptions>? options = null)
    {
        _http = http;
        _log = log;
        _endpoints = (options?.Value ?? new OverpassOptions()).EffectiveEndpoints.ToArray();
        _http.Timeout = TimeSpan.FromSeconds(40);
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var radius = SearchRadius.Clamp(radiusMeters);
        var filters = string.Join(
            "",
            IntentPlaceTypes.OsmTags(intent).Select(t =>
                $"nwr[\"{Escape(t.Key)}\"=\"{Escape(t.Value)}\"](around:{radius.ToString(CultureInfo.InvariantCulture)},{Fmt(latitude)},{Fmt(longitude)});"));
        var query = $"[out:json][timeout:12];({filters});out center 80;";
        var json = await PostAsync(query, cancellationToken, primaryBudgetSeconds: 12);
        return ParseList(json, latitude, longitude).Take(80).ToList();
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
        try
        {
            var json = await PostAsync(body, cancellationToken, primaryBudgetSeconds: 8, takeEndpoints: 1);
            return ParseList(json, 0, 0).FirstOrDefault();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
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
        return ParseList(json, latitude, longitude).Take(40).ToList();
    }

    /// <summary>
    /// Birincil uç bu süre içinde cevap vermezse sıradaki uç paralel başlar; ilk dolu cevap kullanılır, diğerleri iptal edilir.
    /// Önceden uçlar sırayla denendiği için yavaş bir birincil uç ilk açılışı 20+ saniyeye çıkarabiliyordu.
    /// </summary>
    internal TimeSpan HedgeDelay { get; set; } = TimeSpan.FromSeconds(3.5);

    private async Task<JsonElement> PostAsync(
        string query,
        CancellationToken cancellationToken,
        int primaryBudgetSeconds = 22,
        int? takeEndpoints = null)
    {
        var uris = takeEndpoints is null ? _endpoints : _endpoints.Take(takeEndpoints.Value).ToArray();
        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new List<Task<JsonElement?>>();
        var next = 0;

        Task<JsonElement?> Start()
        {
            var i = next++;
            return TryEndpointAsync(uris[i], query, i == 0 ? primaryBudgetSeconds : 10, race.Token);
        }

        running.Add(Start());
        while (running.Count > 0)
        {
            Task finished;
            if (next < uris.Length)
            {
                var hedge = Task.Delay(HedgeDelay, race.Token);
                finished = await Task.WhenAny(running.Cast<Task>().Append(hedge));
                if (finished == hedge)
                {
                    if (!hedge.IsCanceled)
                    {
                        running.Add(Start());
                    }

                    continue;
                }
            }
            else
            {
                finished = await Task.WhenAny(running);
            }

            var attempt = (Task<JsonElement?>)finished;
            running.Remove(attempt);
            if (await attempt is { } json)
            {
                race.Cancel(); // kalan istekler bırakılsın
                return json;
            }

            if (running.Count == 0 && next < uris.Length)
            {
                running.Add(Start()); // hızlı hata: beklemeden sıradakine geç
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new HttpRequestException("Overpass unavailable");
    }

    /// <summary>Tek uca istek. Başarısızlık (hata kodu, boş sonuç, zaman aşımı) null döner ve günlüğe yazılır.</summary>
    private async Task<JsonElement?> TryEndpointAsync(Uri uri, string query, int budgetSeconds, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain")
            };
            request.Headers.TryAddWithoutValidation("User-Agent", HttpIdentity.UserAgent);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
            using var response = await _http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cts.Token);
                _log.LogWarning("Overpass {Uri} {Status}: {Body}", uri, (int)response.StatusCode, err);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            var count = json.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array
                ? elements.GetArrayLength()
                : 0;
            if (count == 0)
            {
                _log.LogWarning("Overpass {Uri} returned no elements", uri);
                return null;
            }

            return json;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Overpass endpoint failed {Uri}", uri);
            }

            return null;
        }
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
            hoursText,
            Tag(tags, hasTags, "cuisine"),
            Tag(tags, hasTags, "outdoor_seating"),
            Tag(tags, hasTags, "internet_access"));
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
