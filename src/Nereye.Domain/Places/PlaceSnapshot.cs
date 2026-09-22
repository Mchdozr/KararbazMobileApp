namespace Nereye.Domain.Places;

public sealed record PlaceHour(DayOfWeek Day, TimeOnly Open, TimeOnly Close);

public sealed record PlaceSnapshot(
    string PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> Types,
    bool? OpenNow,
    string? PhotoReference,
    double? GoogleRating,
    int? GoogleUserRatingCount,
    string? GoogleMapsUri,
    IReadOnlyList<PlaceHour> Hours,
    string Attribution,
    string? Website = null,
    string? Wikidata = null,
    string? Phone = null,
    string? OpeningHoursText = null,
    string? Cuisine = null,
    string? OutdoorSeating = null,
    string? InternetAccess = null);
