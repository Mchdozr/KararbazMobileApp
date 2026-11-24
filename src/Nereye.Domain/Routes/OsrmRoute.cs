using System.Text.Json;

namespace Nereye.Domain.Routes;

public sealed record RoutePoint(double Latitude, double Longitude);

public sealed record RouteResult(
    string Mode,
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<RoutePoint> Points);

public static class OsrmRoute
{
    public static RouteResult? Parse(JsonElement json, string mode)
    {
        if (!json.TryGetProperty("code", out var code) || !string.Equals(code.GetString(), "Ok", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!json.TryGetProperty("routes", out var routes) || routes.ValueKind != JsonValueKind.Array || routes.GetArrayLength() == 0)
        {
            return null;
        }

        var first = routes[0];
        if (!first.TryGetProperty("distance", out var dist) || !first.TryGetProperty("duration", out var dur))
        {
            return null;
        }

        var points = new List<RoutePoint>();
        if (first.TryGetProperty("geometry", out var geometry)
            && geometry.TryGetProperty("coordinates", out var coords)
            && coords.ValueKind == JsonValueKind.Array)
        {
            foreach (var pair in coords.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2)
                {
                    continue;
                }

                points.Add(new RoutePoint(pair[1].GetDouble(), pair[0].GetDouble()));
            }
        }

        var distance = dist.GetDouble();
        var duration = dur.GetDouble();
        // Genel OSRM sunucusu (router.project-osrm.org) yalnızca araç profilini taşır; yaya isteğine araç süresi döner.
        // Yürüyüşte saatte ~7 km'yi aşan hız gerçek olamaz: süre mesafe ve ortalama yürüme hızından hesaplanır.
        if (IsFoot(mode) && distance > 0 && (duration <= 0 || distance / duration > MaxWalkingMetersPerSecond))
        {
            duration = Math.Round(distance / WalkingMetersPerSecond, 1);
        }

        return new RouteResult(mode, distance, duration, points);
    }

    /// <summary>Ortalama yürüme hızı (~4,8 km/sa).</summary>
    public const double WalkingMetersPerSecond = 1.33;

    private const double MaxWalkingMetersPerSecond = 2.0;

    private static bool IsFoot(string mode) =>
        mode.Equals("foot", StringComparison.OrdinalIgnoreCase) || mode.Equals("walking", StringComparison.OrdinalIgnoreCase);

    public static string Profile(string mode) =>
        mode.Equals("foot", StringComparison.OrdinalIgnoreCase) || mode.Equals("walking", StringComparison.OrdinalIgnoreCase)
            ? "walking"
            : "driving";
}
