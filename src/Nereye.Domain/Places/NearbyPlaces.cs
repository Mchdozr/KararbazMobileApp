using Nereye.Domain.Geo;

namespace Nereye.Domain.Places;

public static class NearbyPlaces
{
    public static IReadOnlyList<PlaceSnapshot> WithinRadius(
        IReadOnlyList<PlaceSnapshot> places,
        double latitude,
        double longitude,
        int radiusMeters)
    {
        var radius = SearchRadius.Clamp(radiusMeters);
        return places
            .Where(p => GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude) <= radius)
            .ToList();
    }
}
