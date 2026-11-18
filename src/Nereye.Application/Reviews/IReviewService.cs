using Nereye.Application.Recommendations;

namespace Nereye.Application.Reviews;

public interface IReviewService
{
    /// <summary><paramref name="viewerId"/> verilirse onun engellediği kullanıcıların yorumları listelenmez.</summary>
    Task<IReadOnlyList<ReviewDto>> ListAsync(string placeId, Guid? viewerId = null, CancellationToken cancellationToken = default);
    /// <summary>Kullanıcı başına mekan başına tek yorum: ikinci yazış öncekini günceller.</summary>
    Task<ReviewDto> WriteAsync(string placeId, Guid userId, WriteReviewRequest request, CancellationToken cancellationToken = default);
    Task FavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
    Task UnfavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecommendationItemDto>> ListFavoritesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ReportAsync(string placeId, Guid userId, string reason, Guid? reviewId, string? photoUrl = null, CancellationToken cancellationToken = default);
    Task BlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task UnblockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BlockedUserDto>> BlockedAsync(Guid blockerId, CancellationToken cancellationToken = default);
    Task ReplyAsync(Guid reviewId, Guid ownerUserId, string text, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MyReviewDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsFavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default);
}
