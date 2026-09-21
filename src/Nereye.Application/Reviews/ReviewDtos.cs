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
    DateTime CreatedAt);

public sealed record WriteReviewRequest(int Stars, string? Text, IReadOnlyList<VibeTag> Tags, IReadOnlyList<string> PhotoUrls);
