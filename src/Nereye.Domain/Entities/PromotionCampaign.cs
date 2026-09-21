namespace Nereye.Domain.Entities;

public enum CampaignStatus
{
    Draft,
    AwaitingPayment,
    Active,
    Expired,
    Cancelled
}

public sealed class PromotionCampaign
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Intent TargetIntent { get; set; }
    public required string TargetGeohash { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal DailyBudget { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
