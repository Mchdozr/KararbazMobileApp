using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Overpass (public OSM sunucuları) önünde uzun süreli önbellek. Yalnızca OSM verisi saklanır;
/// Google içeriği bu katmana hiç girmez, bu yüzden süreler Google şartından bağımsız uzun tutulur.
/// Hata önbelleğe yazılmaz; boş sonuç kısa süre (PlacesCacheTtl.Empty) tutulur.
/// Liste iki günden eskiyse (OsmNearbySoft) hemen sunulur ve arka planda tazelenir: kullanıcı Overpass'i beklemez.
/// </summary>
public sealed class CachedOsmPlacesClient : IPlacesClient
{
    private readonly IPlacesClient _inner;
    private readonly IPlacesCache _cache;
    private readonly CacheRefresher? _refresher;

    public CachedOsmPlacesClient(IPlacesClient inner, IPlacesCache cache, CacheRefresher? refresher = null)
    {
        _inner = inner;
        _cache = cache;
        _refresher = refresher;
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var key = Key(latitude, longitude, intent, radiusMeters);
        var hit = await _cache.TryGetAsync<List<PlaceSnapshot>>(key, cancellationToken);
        if (hit is not null)
        {
            var stale = false;
            if (_refresher is not null && hit.Count > 0)
            {
                stale = await _refresher.RevalidateIfStaleAsync(
                    key,
                    PlacesCacheTtl.OsmNearby,
                    PlacesCacheTtl.OsmNearbySoft,
                    ct => FetchAsync(_inner, latitude, longitude, intent, radiusMeters, ct),
                    KeepNonEmpty,
                    cancellationToken: cancellationToken);
            }

            Telemetry.NereyeMetrics.CacheLookup("osm", stale ? "stale" : "hit");
            return hit;
        }

        Telemetry.NereyeMetrics.CacheLookup("osm", "miss");

        var fetched = false;
        var list = await _cache.GetOrSetAsync(
            key,
            PlacesCacheTtl.OsmNearby,
            ct =>
            {
                fetched = true;
                return FetchAsync(_inner, latitude, longitude, intent, radiusMeters, ct);
            },
            cancellationToken);
        if (fetched && list.Count > 0 && _refresher is not null)
        {
            await _refresher.MarkFreshAsync(key, PlacesCacheTtl.OsmNearby, cancellationToken);
        }

        return list;
    }

    /// <summary>OSM listesinin önbellek anahtarı (ısıtma işi de aynısını kullanır).</summary>
    internal static string Key(double latitude, double longitude, Intent intent, int radiusMeters) =>
        PlacesCacheKey.OsmNearby(Geohash.Encode(latitude, longitude, 6), intent, radiusMeters);

    internal static async Task<List<PlaceSnapshot>> FetchAsync(
        IPlacesClient source,
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken) =>
        (await source.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken)).ToList();

    /// <summary>Boş sonuç iyi bir listenin üstüne yazılmaz (geçici Overpass boşluğu).</summary>
    internal static bool KeepNonEmpty(List<PlaceSnapshot> list) => list.Count > 0;

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var key = PlacesCacheKey.OsmDetails(placeId);
        var hit = await _cache.TryGetAsync<PlaceSnapshot>(key, cancellationToken);
        if (hit is not null)
        {
            return hit;
        }

        var found = await _inner.DetailsAsync(placeId, cancellationToken);
        if (found is not null)
        {
            await _cache.SetAsync(key, PlacesCacheTtl.OsmDetails, found, cancellationToken);
        }

        return found;
    }

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        _inner.TextSearchAsync(query, latitude, longitude, cancellationToken);
}
