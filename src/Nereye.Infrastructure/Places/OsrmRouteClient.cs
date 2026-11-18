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
        _http.BaseAddress ??= new Uri("https://router.project-osrm.org/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HttpIdentity.UserAgent);
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
        var path =
            $"route/v1/{profile}/{Fmt(fromLng)},{Fmt(fromLat)};{Fmt(toLng)},{Fmt(toLat)}?overview=full&geometries=geojson";
        using var response = await _http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return OsrmRoute.Parse(json, mode.Equals("driving", StringComparison.OrdinalIgnoreCase) ? "driving" : "foot");
    }

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
