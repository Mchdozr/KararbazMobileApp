namespace Nereye.Domain.Entities;

public sealed class ReviewReply
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; }
}
