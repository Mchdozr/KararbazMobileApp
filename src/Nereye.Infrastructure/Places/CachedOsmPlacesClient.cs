using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Overpass (public OSM sunucuları) önünde uzun süreli önbellek. Yalnızca OSM verisi saklanır;
/// Google içeriği bu katmana hiç girmez, bu yüzden süreler Google şartından bağımsız uzun tutulur.
/// Hata önbelleğe yazılmaz; boş sonuç kısa süre (PlacesCacheTtl.Empty) tutulur.
/// </summary>
public sealed class CachedOsmPlacesClient : IPlacesClient
{
    private readonly IPlacesClient _inner;
    private readonly IPlacesCache _cache;

    public CachedOsmPlacesClient(IPlacesClient inner, IPlacesCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default) =>
        await _cache.GetOrSetAsync<List<PlaceSnapshot>>(
            PlacesCacheKey.OsmNearby(Geohash.Encode(latitude, longitude, 6), intent, radiusMeters),
            PlacesCacheTtl.OsmNearby,
            async ct => (await _inner.NearbyAsync(latitude, longitude, intent, radiusMeters, ct)).ToList(),
            cancellationToken);

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
