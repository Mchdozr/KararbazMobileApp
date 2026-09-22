using Nereye.Application.Recommendations;

namespace Nereye.Application.Venues;

public interface IVenueQueryService
{
    Task<VenueDetailDto?> GetAsync(string placeId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecommendationItemDto>> SearchAsync(string query, double latitude, double longitude, CancellationToken cancellationToken = default);
    Task UpdateOwnerAsync(string placeId, Guid userId, string? displayName, string? description, CancellationToken cancellationToken = default);
    Task<string> AddOwnerPhotoAsync(string placeId, Guid userId, byte[] bytes, CancellationToken cancellationToken = default);
    Task<string?> GetPhotoAsync(string placeId, CancellationToken cancellationToken = default);
}
