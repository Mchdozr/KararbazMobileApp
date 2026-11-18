using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class NominatimPlacesClient : IPlacesClient, IGeoSuggest
{
    private static readonly TimeSpan SuggestWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SearchWait = TimeSpan.FromSeconds(10);
    private readonly HttpClient _http;
    private readonly NominatimGate _gate;
    private readonly IPlacesCache _cache;

    public NominatimPlacesClient(HttpClient http, NominatimGate gate, IPlacesCache cache)
    {
        _http = http;
        _gate = gate;
        _cache = cache;
        _http.BaseAddress ??= new Uri("https://nominatim.openstreetmap.org/");
        _http.Timeout = TimeSpan.FromSeconds(12);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HttpIdentity.UserAgent);
        }

        if (!_http.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "tr");
        }
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var q = IntentPlaceTypes.NominatimQuery(intent);
        return await SearchAsync(q, latitude, longitude, radiusMeters, cancellationToken);
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        if (!TryParseOsmId(placeId, out var type, out var id))
        {
            return null;
        }

        var prefix = type switch
        {
            "node" => "N",
            "way" => "W",
            "relation" => "R",
            _ => "N"
        };
        var json = await GetJsonAsync($"lookup?osm_ids={prefix}{id}&format=json&addressdetails=1&extratags=1", SearchWait, cancellationToken);
        return NominatimPlaceMapper.ParseArray(json, 0, 0, SearchRadius.MaxMeters).FirstOrDefault();
    }

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);
        }

        return SearchAsync(q, latitude, longitude, 4000, cancellationToken);
    }

    public async Task<IReadOnlyList<GeoHit>> SuggestAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return [];
        }

        // Nominatim politikası sonuçların önbelleğe alınmasını ister: aynı yazım ikinci kez dışarı gitmez.
        // Önbellekten dönen liste paylaşılır; değiştirilmez.
        return await _cache.GetOrSetAsync<IReadOnlyList<GeoHit>>(
            PlacesCacheKey.Suggest(q),
            PlacesCacheTtl.Suggest,
            async ct =>
            {
                var path =
                    $"search?q={Uri.EscapeDataString(q)}&format=json&limit=20&addressdetails=1&dedupe=1&countrycodes=tr";
                var json = await GetJsonAsync(path, SuggestWait, ct);
                return GeoSuggestMapper.Parse(json).ToList();
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<PlaceSnapshot>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        var viewbox = NominatimViewBox.Format(latitude, longitude, radiusMeters);
        var path =
            $"search?q={Uri.EscapeDataString(query)}&format=json&limit=50&bounded=1&addressdetails=1&extratags=1&viewbox={viewbox}";
        var json = await GetJsonAsync(path, SearchWait, cancellationToken);
        return NominatimPlaceMapper.ParseArray(json, latitude, longitude, radiusMeters);
    }

    /// <summary>Her Nominatim isteği aynı süreç kapısından geçer (saniyede en fazla bir).</summary>
    private Task<JsonElement> GetJsonAsync(string path, TimeSpan maxWait, CancellationToken cancellationToken) =>
        _gate.RunAsync(
            async ct =>
            {
                using var response = await _http.GetAsync(path, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            },
            maxWait,
            cancellationToken);

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
}
