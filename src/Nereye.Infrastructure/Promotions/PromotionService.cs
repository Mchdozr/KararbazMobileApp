using Microsoft.EntityFrameworkCore;
using Nereye.Application.Payments;
using Nereye.Application.Promotions;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Promotions;

public sealed class PromotionService : IPromotionService
{
    private readonly NereyeDbContext _db;
    private readonly IPaymentGateway _payments;
    private readonly Application.Common.IClock _clock;

    public PromotionService(NereyeDbContext db, IPaymentGateway payments, Application.Common.IClock clock)
    {
        _db = db;
        _payments = payments;
        _clock = clock;
    }

    public async Task<PromotionDto> CreateAsync(Guid ownerUserId, CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == request.PlaceId, cancellationToken)
                    ?? throw new InvalidOperationException("Mekan yok.");
        if (venue.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException("Sadece sahip kampanya açabilir.");
        }

        var now = _clock.UtcNow;
        var campaign = new PromotionCampaign
        {
            Id = Guid.NewGuid(),
            VenueId = venue.Id,
            OwnerUserId = ownerUserId,
            TargetIntent = request.TargetIntent,
            TargetGeohash = string.IsNullOrWhiteSpace(venue.Geohash) ? request.TargetGeohash : venue.Geohash,
            StartAt = now,
            EndAt = now.AddDays(Math.Clamp(request.Days, 1, 30)),
            DailyBudget = request.DailyBudget,
            Status = CampaignStatus.AwaitingPayment,
            CreatedAt = now
        };
        _db.PromotionCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(campaign, request.PlaceId);
    }

    public async Task<PromotionDto> CheckoutAsync(Guid ownerUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.PromotionCampaigns.FirstOrDefaultAsync(
                           c => c.Id == campaignId && c.OwnerUserId == ownerUserId,
                           cancellationToken)
                       ?? throw new InvalidOperationException("Kampanya yok.");
        var days = Math.Max(1, (int)Math.Ceiling((campaign.EndAt - campaign.StartAt).TotalDays));
        var amount = campaign.DailyBudget * days;
        var result = await _payments.ChargeAsync(
            new PaymentChargeRequest(campaign.Id, amount, "Nereye öne çıkarma"),
            cancellationToken);
        _db.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Provider = result.Provider,
            ExternalId = result.ExternalId,
            Amount = amount,
            Status = result.Succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            CreatedAt = _clock.UtcNow
        });
        if (!result.Succeeded)
        {
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Ödeme başarısız.");
        }

        campaign.Status = CampaignStatus.Active;
        await _db.SaveChangesAsync(cancellationToken);
        var venue = await _db.Venues.FirstAsync(v => v.Id == campaign.VenueId, cancellationToken);
        return ToDto(campaign, venue.GooglePlaceId);
    }

    public async Task<IReadOnlyList<PromotionDto>> MineAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.PromotionCampaigns
            .Where(c => c.OwnerUserId == ownerUserId)
            .Join(_db.Venues, c => c.VenueId, v => v.Id, (c, v) => new { c, v.GooglePlaceId })
            .ToListAsync(cancellationToken);
        return rows.Select(r => ToDto(r.c, r.GooglePlaceId)).ToList();
    }

    private static PromotionDto ToDto(PromotionCampaign c, string placeId) =>
        new(c.Id, placeId, c.TargetIntent, c.TargetGeohash, c.StartAt, c.EndAt, c.Status, c.DailyBudget);
}
