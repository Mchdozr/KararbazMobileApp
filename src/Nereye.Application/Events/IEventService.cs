namespace Nereye.Application.Events;

public sealed record AddEventRequest(string? Title, DateTime StartsAt, DateTime? EndsAt);

public sealed record VenueEventDto(Guid Id, string Title, DateTime StartsAt, DateTime? EndsAt);

public sealed record TonightEventDto(
    Guid Id,
    string Title,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool Live,
    string PlaceId,
    string VenueName,
    string? PhotoUrl,
    double DistanceMeters);

public sealed record TonightResponse(IReadOnlyList<TonightEventDto> Items);

public interface IEventService
{
    Task<TonightResponse> TonightAsync(double latitude, double longitude, int radiusMeters, CancellationToken cancellationToken = default);

    Task<VenueEventDto> AddAsync(string placeId, Guid ownerId, AddEventRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string placeId, Guid ownerId, Guid eventId, CancellationToken cancellationToken = default);
}
