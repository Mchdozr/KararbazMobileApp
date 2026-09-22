namespace Nereye.Application.Routes;

public sealed record RoutePointDto(double Latitude, double Longitude);

public sealed record RouteDto(
    string Mode,
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<RoutePointDto> Points);

public interface IRouteService
{
    Task<RouteDto?> GetAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        string mode,
        CancellationToken cancellationToken = default);
}
