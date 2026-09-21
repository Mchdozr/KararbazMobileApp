namespace Nereye.Domain.Entities;

public sealed class Review
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public Guid UserId { get; set; }
    public int Stars { get; set; }
    public string? Text { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
    public Venue? Venue { get; set; }
    public ICollection<ReviewTag> Tags { get; set; } = new List<ReviewTag>();
    public ICollection<ReviewPhoto> Photos { get; set; } = new List<ReviewPhoto>();
    public ReviewReply? Reply { get; set; }
}
