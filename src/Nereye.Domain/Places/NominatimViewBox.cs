using System.Globalization;

namespace Nereye.Domain.Places;

public static class NominatimViewBox
{
    public static string Format(double latitude, double longitude, int radiusMeters)
    {
        var radius = SearchRadius.Clamp(radiusMeters);
        var dLat = radius / 111_320d;
        var cos = Math.Max(0.2, Math.Cos(latitude * Math.PI / 180d));
        var dLng = radius / (111_320d * cos);
        return string.Join(',',
            Fmt(longitude - dLng),
            Fmt(latitude + dLat),
            Fmt(longitude + dLng),
            Fmt(latitude - dLat));
    }

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
