namespace Nereye.Domain.Geo;

public static class GeoMath
{
    public const double MaxWalkMeters = 1500;
    private const double EarthRadiusMeters = 6_371_000;

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static double Proximity01(double distanceMeters)
    {
        if (distanceMeters <= 0)
        {
            return 1;
        }

        if (distanceMeters >= MaxWalkMeters)
        {
            return 0;
        }

        return 1 - (distanceMeters / MaxWalkMeters);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
