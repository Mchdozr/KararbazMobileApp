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
    string? InternetAccess = null);

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
    IReadOnlyList<string>? AmenityChips = null);

public sealed record CultureEventDto(Guid Id, string Title, DateTime StartsAt, DateTime? EndsAt);
