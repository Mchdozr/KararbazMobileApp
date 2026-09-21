namespace Nereye.Domain.Entities;

public sealed class VenueTag
{
    public Guid VenueId { get; set; }
    public VibeTag Tag { get; set; }
    public Venue? Venue { get; set; }
}
