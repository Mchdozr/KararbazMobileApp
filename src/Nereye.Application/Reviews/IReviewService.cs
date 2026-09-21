using Nereye.Application.Recommendations;

namespace Nereye.Application.Reviews;

public interface IReviewService
{
    Task<IReadOnlyList<ReviewDto>> ListAsync(string placeId, CancellationToken cancellationToken = default);
    Task<ReviewDto> WriteAsync(string placeId, Guid userId, WriteReviewRequest request, CancellationToken cancellationToken = default);
    Task FavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
    Task UnfavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecommendationItemDto>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ReportAsync(string placeId, Guid userId, string reason, Guid? reviewId, CancellationToken cancellationToken = default);
    Task ReplyAsync(Guid reviewId, Guid ownerUserId, string text, CancellationToken cancellationToken = default);
    Task<bool> IsFavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
}
