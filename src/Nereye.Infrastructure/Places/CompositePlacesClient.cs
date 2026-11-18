using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class CompositePlacesClient : IPlacesClient, IRichPlaceDetails
{
    private readonly IPlacesClient _nearby;
    private readonly IPlacesClient _search;
    private readonly IPlacesClient _details;

    public CompositePlacesClient(IPlacesClient nearby, IPlacesClient search, IPlacesClient details)
    {
        _nearby = nearby;
        _search = search;
        _details = details;
    }

    public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default) =>
        _nearby.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        _search.TextSearchAsync(query, latitude, longitude, cancellationToken);

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var found = await _details.DetailsAsync(placeId, cancellationToken);
        if (found is not null)
        {
            return found;
        }

        return await _search.DetailsAsync(placeId, cancellationToken);
    }

    public async Task<PlaceSnapshot?> RichDetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var found = _details is IRichPlaceDetails rich
            ? await rich.RichDetailsAsync(placeId, cancellationToken)
            : await _details.DetailsAsync(placeId, cancellationToken);
        return found ?? await _search.DetailsAsync(placeId, cancellationToken);
    }
}
