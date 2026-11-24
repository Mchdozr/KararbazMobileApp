using Nereye.Application.Places;
using Nereye.Application.Routes;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class RouteService : IRouteService
{
    private readonly OsrmRouteClient _osrm;
    private readonly IPlacesCache _cache;

    public RouteService(OsrmRouteClient osrm, IPlacesCache cache)
    {
        _osrm = osrm;
        _cache = cache;
    }

    /// <summary>Önbellekte "rota yok" da saklanır (kısa süre): dış servis kapalıyken her mekan açılışı onu yeniden yormaz.</summary>
    public sealed record CachedRoute(RouteDto? Route);

    public async Task<RouteDto?> GetAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var normalized = mode.Equals("driving", StringComparison.OrdinalIgnoreCase) ? "driving" : "foot";
        var key = PlacesCacheKey.Route(fromLat, fromLng, toLat, toLng, normalized);
        var hit = await _cache.TryGetAsync<CachedRoute>(key, cancellationToken);
        if (hit is not null)
        {
            return hit.Route;
        }

        Domain.Routes.RouteResult? found;
        try
        {
            found = await _osrm.FindAsync(fromLat, fromLng, toLat, toLng, normalized, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or System.Text.Json.JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Rota bir ek bilgi: dış servis kapalı ya da yavaşsa mekan sayfası rotasız açılır, 500 dönmez.
            await _cache.SetAsync(key, PlacesCacheTtl.Empty, new CachedRoute(null), cancellationToken);
            return null;
        }

        var route = found is null
            ? null
            : new RouteDto(
                found.Mode,
                Math.Round(found.DistanceMeters, 1),
                Math.Round(found.DurationSeconds, 1),
                found.Points.Select(p => new RoutePointDto(p.Latitude, p.Longitude)).ToList());
        await _cache.SetAsync(key, route is null ? PlacesCacheTtl.Empty : PlacesCacheTtl.Route, new CachedRoute(route), cancellationToken);
        return route;
    }
}
