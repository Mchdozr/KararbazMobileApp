namespace Nereye.Domain.Entities;

public enum ClaimStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class VenueClaim
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public Guid UserId { get; set; }
    public required string Message { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
