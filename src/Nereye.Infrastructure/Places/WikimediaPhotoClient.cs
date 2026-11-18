using System.Net.Http.Json;
using System.Text.Json;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class WikimediaPhotoClient
{
    private readonly HttpClient _http;

    public WikimediaPhotoClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(8);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HttpIdentity.UserAgent);
        }
    }

    public async Task<string?> FindWikidataAsync(string? qid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qid))
        {
            return null;
        }

        var id = qid.Trim();
        var cut = id.IndexOfAny([';', '|', ',']);
        if (cut > 0)
        {
            id = id[..cut];
        }

        if (id.Length < 2 || (id[0] is not 'Q' and not 'q') || !id[1..].All(char.IsDigit))
        {
            return null;
        }

        var path =
            $"https://www.wikidata.org/w/api.php?action=wbgetclaims&entity={Uri.EscapeDataString(id.ToUpperInvariant())}&property=P18&format=json";
        using var response = await _http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return PlacePhoto.FromWikidataClaims(json);
    }

    public async Task<string?> FindNearbyAsync(
        double latitude,
        double longitude,
        string venueName,
        CancellationToken cancellationToken = default)
    {
        var lat = latitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var path =
            $"https://commons.wikimedia.org/w/api.php?action=query&list=geosearch&gscoord={lat}|{lng}&gsradius=120&gsnamespace=6&gslimit=8&format=json";
        using var response = await _http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!json.TryGetProperty("query", out var query)
            || !query.TryGetProperty("geosearch", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var hits = new List<PlacePhoto.CommonsGeoHit>();
        foreach (var row in rows.EnumerateArray())
        {
            var title = row.TryGetProperty("title", out var t) ? t.GetString() : null;
            var dist = row.TryGetProperty("dist", out var distEl) && distEl.ValueKind == JsonValueKind.Number
                ? distEl.GetDouble()
                : 999;
            hits.Add(new PlacePhoto.CommonsGeoHit(title, dist));
        }

        return PlacePhoto.PickCommonsNearby(hits, venueName);
    }
}
