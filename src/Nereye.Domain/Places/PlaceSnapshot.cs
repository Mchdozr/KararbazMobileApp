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
    string? InternetAccess = null,
    // Zengin içerik (Google Places anahtarı varsa dolar; yoksa null kalır).
    string? Summary = null,
    int? PriceLevel = null,
    string? Category = null,
    IReadOnlyList<string>? Photos = null,
    IReadOnlyList<string>? Features = null,
    IReadOnlyList<PlaceReview>? Reviews = null);

/// <summary>Dış kaynaktan (Google) gelen kullanıcı yorumu. Nereye yorumlarından ayrı gösterilir.</summary>
public sealed record PlaceReview(
    string Author,
    int Rating,
    string? Text,
    string? When,
    string? AuthorPhotoUrl = null);
