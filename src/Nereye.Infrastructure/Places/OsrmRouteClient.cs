using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Nereye.Domain.Routes;

namespace Nereye.Infrastructure.Places;

public sealed class OsrmRouteClient
{
    private readonly HttpClient _http;

    public OsrmRouteClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(8);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Nereye/1.0 (https://nereye.app)");
        }
    }

    public async Task<RouteResult?> FindAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var profile = OsrmRoute.Profile(mode);
        var root = profile == "driving"
            ? "https://routing.openstreetmap.de/routed-car/"
            : "https://routing.openstreetmap.de/routed-foot/";
        var path =
            $"route/v1/{profile}/{Fmt(fromLng)},{Fmt(fromLat)};{Fmt(toLng)},{Fmt(toLat)}?overview=full&geometries=geojson";
        using var response = await _http.GetAsync(new Uri(new Uri(root), path), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return OsrmRoute.Parse(json, mode.Equals("driving", StringComparison.OrdinalIgnoreCase) ? "driving" : "foot");
    }

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
