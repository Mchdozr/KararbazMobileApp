namespace Nereye.Domain.Places;

public static class PlacesCacheKey
{
    public static readonly int[] RadiusBuckets =
        [500, 1000, 1500, 2000, 3000, 5000, 8000, 10_000, 15_000, 20_000, 30_000, 50_000, 100_000];

    public static string Nearby(string geohash, Intent intent, int radiusMeters)
    {
        var cell = geohash.Length <= 6 ? geohash : geohash[..6];
        return $"places:nearby:v4:{cell}:{intent.ToString().ToLowerInvariant()}:{BucketMeters(radiusMeters)}";
    }

    public static string Details(string placeId) => $"places:details:v2:{placeId}";

    public static string TextSearch(string geohash, string query) =>
        $"places:text:v2:{geohash}:{query.Trim().ToLowerInvariant()}";

    public static int BucketMeters(int radiusMeters)
    {
        var n = SearchRadius.Clamp(radiusMeters);
        foreach (var bucket in RadiusBuckets)
        {
            if (bucket >= n)
            {
                return bucket;
            }
        }

        return RadiusBuckets[^1];
    }

    public static IEnumerable<int> ServingBuckets(int radiusMeters)
    {
        var min = BucketMeters(radiusMeters);
        foreach (var bucket in RadiusBuckets)
        {
            if (bucket >= min)
            {
                yield return bucket;
            }
        }
    }
}
