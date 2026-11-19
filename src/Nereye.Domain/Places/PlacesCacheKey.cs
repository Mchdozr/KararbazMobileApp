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

    /// <summary>Mekanın kart (kapak) görseli: detayda ya da görsel aramasında bulunan ilk gerçek kare.</summary>
    public static string Cover(string placeId) => $"places:cover:v1:{placeId}";

    /// <summary>Konum önerisi. Büyük/küçük harf ve fazla boşluk aynı anahtara düşer.</summary>
    public static string Suggest(string query) =>
        "places:suggest:v1:" + string.Join(' ', query.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Yalnızca OpenStreetMap kaynaklı (Overpass) yakın sonuçlar; Google içeriği taşımaz.</summary>
    public static string OsmNearby(string geohash, Intent intent, int radiusMeters)
    {
        var cell = geohash.Length <= 6 ? geohash : geohash[..6];
        return $"places:osm:nearby:v1:{cell}:{intent.ToString().ToLowerInvariant()}:{BucketMeters(radiusMeters)}";
    }

    public static string OsmDetails(string placeId) => $"places:osm:details:v1:{placeId}";

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
