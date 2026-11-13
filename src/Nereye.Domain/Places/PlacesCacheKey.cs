namespace Nereye.Domain.Places;

public static class PlacesCacheKey
{
    public static readonly int[] RadiusBuckets =
        [500, 1000, 1500, 2000, 3000, 5000, 8000, 10_000, 15_000, 20_000, 30_000, 50_000, 100_000];

    public static string Nearby(string geohash, Intent intent, int radiusMeters)
    {
        var cell = geohash.Length <= 6 ? geohash : geohash[..6];
        return $"places:nearby:v6:{cell}:{intent.ToString().ToLowerInvariant()}:{BucketMeters(radiusMeters)}";
    }

    /// <summary>Liste aramasından gelen, kart için yeten kayıt.</summary>
    public static string Details(string placeId) => $"places:details:v4:{placeId}";

    /// <summary>Detay sayfası için zenginleştirilmiş kayıt (özet, yorum, olanak, galeri).</summary>
    public static string Rich(string placeId) => $"places:rich:v2:{placeId}";

    public static string PhotoUri(string name, int width) => $"places:photouri:v1:{width}:{name}";

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
