using System.Globalization;

namespace Nereye.Domain.Places;

public static class PlacesCacheKey
{
    public static string Nearby(string geohash, Intent intent, int radiusMeters, double latitude = 0, double longitude = 0)
    {
        var cell = $"{latitude.ToString("0.000", CultureInfo.InvariantCulture)}:{longitude.ToString("0.000", CultureInfo.InvariantCulture)}";
        return $"places:nearby:{geohash}:{intent.ToString().ToLowerInvariant()}:{radiusMeters}:{cell}";
    }

    public static string Details(string placeId) => $"places:details:{placeId}";

    public static string TextSearch(string geohash, string query) =>
        $"places:text:{geohash}:{query.Trim().ToLowerInvariant()}";
}
