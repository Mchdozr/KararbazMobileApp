namespace Nereye.Application.Reviews;

public sealed record ReviewDto(
    Guid Id,
    Guid UserId,
    string? AuthorName,
    int Stars,
    string? Text,
    IReadOnlyList<VibeTag> Tags,
    IReadOnlyList<string> PhotoUrls,
    string? OwnerReply,
    DateTime CreatedAt,
    string? AuthorAvatarUrl = null);

/// <summary>Kullanıcının kendi yorumu, hangi mekana ait olduğuyla birlikte.</summary>
public sealed record MyReviewDto(
    Guid Id,
    string PlaceId,
    string VenueName,
    int Stars,
    string? Text,
    IReadOnlyList<VibeTag> Tags,
    IReadOnlyList<string> PhotoUrls,
    string? OwnerReply,
    DateTime CreatedAt);

public sealed record WriteReviewRequest(int Stars, string? Text, IReadOnlyList<VibeTag>? Tags, IReadOnlyList<string>? PhotoUrls);
