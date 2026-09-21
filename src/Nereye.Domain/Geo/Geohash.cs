namespace Nereye.Domain.Geo;

public static class Geohash
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    public const int RecommendPrecision = 7;

    public static string Encode(double latitude, double longitude, int precision = RecommendPrecision)
    {
        var minLat = -90.0;
        var maxLat = 90.0;
        var minLon = -180.0;
        var maxLon = 180.0;
        var even = true;
        var bit = 0;
        var ch = 0;
        var geohash = new char[precision];
        var idx = 0;

        while (idx < precision)
        {
            if (even)
            {
                var mid = (minLon + maxLon) / 2;
                if (longitude > mid)
                {
                    ch |= 1 << (4 - bit);
                    minLon = mid;
                }
                else
                {
                    maxLon = mid;
                }
            }
            else
            {
                var mid = (minLat + maxLat) / 2;
                if (latitude > mid)
                {
                    ch |= 1 << (4 - bit);
                    minLat = mid;
                }
                else
                {
                    maxLat = mid;
                }
            }

            even = !even;
            if (bit < 4)
            {
                bit++;
            }
            else
            {
                geohash[idx++] = Base32[ch];
                bit = 0;
                ch = 0;
            }
        }

        return new string(geohash);
    }
}
