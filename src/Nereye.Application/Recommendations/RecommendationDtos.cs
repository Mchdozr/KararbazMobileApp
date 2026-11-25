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
    double? GoogleRating = null,
    int? GoogleRatingCount = null,
    int? PriceLevel = null,
    string? Category = null,
    // Sunum anında İstanbul saatiyle hesaplanır: "23:00'e kadar açık", "09:00'da açılıyor".
    string? ClosesAt = null,
    int? ClosesInMinutes = null,
    string? OpensAt = null,
    int? OpensInMinutes = null);

public sealed record CultureEventDto(Guid Id, string Title, DateTime StartsAt, DateTime? EndsAt);

public sealed record RecommendationResponse(
    IReadOnlyList<RecommendationItemDto> Items,
    string Geohash,
    string Attribution,
    bool HasMore = false,
    Weather.WeatherDto? Weather = null);
