using Nereye.Domain.Places;

namespace Nereye.Application.Places;

public interface IPlacesClient
{
    Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default);

    Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
