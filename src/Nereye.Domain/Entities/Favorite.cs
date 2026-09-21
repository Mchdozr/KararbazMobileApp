namespace Nereye.Domain.Entities;

public sealed class Favorite
{
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public DateTime CreatedAt { get; set; }
}
