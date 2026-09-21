namespace Nereye.Domain.Entities;

public sealed class PromotionImpression
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Tapped { get; set; }
}
