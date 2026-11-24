using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nereye.Domain.Routes;

namespace Nereye.Infrastructure.Places;

/// <summary>OSRM rota istemcisi. Yaya ve araç ayrı sunuculara gidebilir (RoutingOptions).</summary>
public sealed class OsrmRouteClient
{
    private readonly HttpClient _http;
    private readonly RoutingOptions _options;

    public OsrmRouteClient(HttpClient http, IOptions<RoutingOptions>? options = null)
    {
        _http = http;
        _options = options?.Value ?? new RoutingOptions();
        _http.Timeout = TimeSpan.FromSeconds(8);
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
        var server = profile == "driving" ? _options.CarBase : _options.FootBase;
        var path = new Uri(server,
            $"route/v1/{profile}/{Fmt(fromLng)},{Fmt(fromLat)};{Fmt(toLng)},{Fmt(toLat)}?overview=full&geometries=geojson");
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
