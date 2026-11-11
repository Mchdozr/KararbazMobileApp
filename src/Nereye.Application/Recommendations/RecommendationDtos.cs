using Nereye.Domain.Recommendation;

namespace Nereye.Application.Recommendations;

public sealed record RecommendationQuery(
    double Latitude,
    double Longitude,
    Intent Intent,
    int RadiusMeters,
    bool OpenNowOnly,
    IReadOnlyList<VibeTag> Vibes,
    Guid? UserId,
    int Skip = 0,
    int Take = 20);

public sealed record RecommendationItemDto(
    string PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    double Score,
    CardKind Kind,
    bool? OpenNow,
    double? NereyeRating,
    int NereyeRatingCount,
    string? PhotoReference,
    string? PhotoUrl,
    IReadOnlyList<VibeTag> Vibes,
    IReadOnlyList<CultureEventDto> UpcomingEvents,
    string Note,
    string? Cuisine = null);

public sealed record CultureEventDto(Guid Id, string Title, DateTime StartsAt, DateTime? EndsAt);

public sealed record RecommendationResponse(
    IReadOnlyList<RecommendationItemDto> Items,
    string Geohash,
    string Attribution,
    bool HasMore = false);
