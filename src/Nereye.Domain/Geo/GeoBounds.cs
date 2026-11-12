namespace Nereye.Domain.Geo;

public static class GeoBounds
{
    public static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude)
        && double.IsFinite(longitude)
        && latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180
        && !(latitude == 0 && longitude == 0);
}
