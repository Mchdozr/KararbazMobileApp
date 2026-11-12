using Nereye.Domain.Places;

namespace Nereye.Application.Places;

public interface IPlacePhotoEnricher
{
    Task<IReadOnlyList<PlaceSnapshot>> EnrichAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaceSnapshot>> EnrichLiteAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default);

    Task<PlaceSnapshot> EnrichOneAsync(PlaceSnapshot place, CancellationToken cancellationToken = default);
}
