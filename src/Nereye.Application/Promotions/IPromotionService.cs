namespace Nereye.Application.Promotions;

public sealed record CreatePromotionRequest(string PlaceId, Intent TargetIntent, string TargetGeohash, int Days, decimal DailyBudget);

public sealed record PromotionDto(
    Guid Id,
    string PlaceId,
    Intent TargetIntent,
    string TargetGeohash,
    DateTime StartAt,
    DateTime EndAt,
    CampaignStatus Status,
    decimal DailyBudget);

public interface IPromotionService
{
    Task<PromotionDto> CreateAsync(Guid ownerUserId, CreatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<PromotionDto> CheckoutAsync(Guid ownerUserId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromotionDto>> MineAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
}
