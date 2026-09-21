namespace Nereye.Domain.Entities;

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed
}

public sealed class PaymentRecord
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
