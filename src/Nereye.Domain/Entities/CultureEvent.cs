namespace Nereye.Domain.Entities;

public sealed class CultureEvent
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public required string Title { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public Venue? Venue { get; set; }
}
