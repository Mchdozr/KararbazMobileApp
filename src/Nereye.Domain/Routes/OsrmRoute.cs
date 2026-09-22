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

        return new RouteResult(mode, dist.GetDouble(), dur.GetDouble(), points);
    }

    public static string Profile(string mode) =>
        mode.Equals("foot", StringComparison.OrdinalIgnoreCase) || mode.Equals("walking", StringComparison.OrdinalIgnoreCase)
            ? "walking"
            : "driving";
}
