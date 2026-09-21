using Microsoft.Extensions.Logging;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class FallbackPlacesClient : IPlacesClient
{
    private readonly IPlacesClient _primary;
    private readonly IPlacesClient _fallback;
    private readonly bool _fallbackOnEmpty;
    private readonly ILogger<FallbackPlacesClient>? _log;

    public FallbackPlacesClient(
        IPlacesClient primary,
        IPlacesClient fallback,
        bool fallbackOnEmpty = true,
        ILogger<FallbackPlacesClient>? log = null)
    {
        _primary = primary;
        _fallback = fallback;
        _fallbackOnEmpty = fallbackOnEmpty;
        _log = log;
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _primary.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
            if (items.Count > 0 || !_fallbackOnEmpty)
            {
                return items;
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Places primary failed, using fallback");
        }

        return await _fallback.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var place = await _primary.DetailsAsync(placeId, cancellationToken);
            if (place is not null)
            {
                return place;
            }
        }
        catch (Exception)
        {
            /* ignore */
        }

        return await _fallback.DetailsAsync(placeId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _primary.TextSearchAsync(query, latitude, longitude, cancellationToken);
            if (items.Count > 0)
            {
                return items;
            }
        }
        catch (Exception)
        {
            /* ignore */
        }

        return await _fallback.TextSearchAsync(query, latitude, longitude, cancellationToken);
    }
}
