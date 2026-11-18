namespace Nereye.Application.Venues;

public sealed record GoogleVenueBlock(
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    bool? OpenNow,
    double? GoogleRating,
    int? GoogleRatingCount,
    string? PhotoReference,
    string? GoogleMapsUri,
    string Attribution,
    IReadOnlyList<string> Types,
    string? Phone = null,
    string? Website = null,
    string? OpeningHours = null,
    string? Cuisine = null,
    string? OutdoorSeating = null,
    string? InternetAccess = null,
    string? Summary = null,
    int? PriceLevel = null,
    string? Category = null,
    IReadOnlyList<string>? PhotoUrls = null,
    IReadOnlyList<string>? Features = null);

/// <summary>Dış kaynaktan (Google) gelen yorum; Nereye yorumlarından ayrı bir bölümde gösterilir.</summary>
public sealed record ExternalReviewDto(string Author, int Rating, string? Text, string? When, string? AuthorPhotoUrl, string? AuthorUri = null);

public sealed record NereyeVenueBlock(
    Guid? VenueId,
    double? RatingAvg,
    int RatingCount,
    bool Claimed,
    string? OwnerDisplayName,
    string? OwnerDescription,
    IReadOnlyList<VibeTag> Vibes,
    IReadOnlyList<string> PhotoUrls,
    string? Geohash = null,
    bool IsFavorite = false);

public sealed record VenueDetailDto(
    string PlaceId,
    GoogleVenueBlock Google,
    NereyeVenueBlock Nereye,
    IReadOnlyList<CultureEventDto> Events,
    string? Note,
    IReadOnlyList<string>? AmenityChips = null,
    IReadOnlyList<ExternalReviewDto>? ExternalReviews = null);

public sealed record CultureEventDto(Guid Id, string Title, DateTime StartsAt, DateTime? EndsAt);
