using Nereye.Application.Routes;

namespace Nereye.Infrastructure.Places;

public sealed class RouteService : IRouteService
{
    private readonly OsrmRouteClient _osrm;

    public RouteService(OsrmRouteClient osrm) => _osrm = osrm;

    public async Task<RouteDto?> GetAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var normalized = mode.Equals("driving", StringComparison.OrdinalIgnoreCase) ? "driving" : "foot";
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
            return null;
        }

        if (found is null)
        {
            return null;
        }

        return new RouteDto(
            found.Mode,
            Math.Round(found.DistanceMeters, 1),
            Math.Round(found.DurationSeconds, 1),
            found.Points.Select(p => new RoutePointDto(p.Latitude, p.Longitude)).ToList());
    }
}
