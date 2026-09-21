using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class NominatimPlacesClient : IPlacesClient, IGeoSuggest
{
    private readonly HttpClient _http;

    public NominatimPlacesClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://nominatim.openstreetmap.org/");
        _http.Timeout = TimeSpan.FromSeconds(12);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Nereye/1.0 (https://nereye.app)");
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
        var json = await GetJsonAsync($"lookup?osm_ids={prefix}{id}&format=json&addressdetails=1&extratags=1", cancellationToken);
        return NominatimPlaceMapper.ParseArray(json, 0, 0, 8000).FirstOrDefault();
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

        var path =
            $"search?q={Uri.EscapeDataString(q)}&format=json&limit=12&addressdetails=1&countrycodes=tr";
        var json = await GetJsonAsync(path, cancellationToken);
        return GeoSuggestMapper.Parse(json);
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
            $"search?q={Uri.EscapeDataString(query)}&format=json&limit=30&bounded=1&addressdetails=1&extratags=1&viewbox={viewbox}";
        var json = await GetJsonAsync(path, cancellationToken);
        return NominatimPlaceMapper.ParseArray(json, latitude, longitude, radiusMeters);
    }

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

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
