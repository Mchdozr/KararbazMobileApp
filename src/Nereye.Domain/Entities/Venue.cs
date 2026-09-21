namespace Nereye.Domain.Entities;

public sealed class Venue
{
    public Guid Id { get; set; }
    public required string GooglePlaceId { get; set; }
    public bool Claimed { get; set; }
    public Guid? OwnerUserId { get; set; }
    public double? NereyeRatingAvg { get; set; }
    public int RatingCount { get; set; }
    public Intent PrimaryIntent { get; set; }
    public string? Geohash { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? OwnerDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<VenueTag> Tags { get; set; } = new List<VenueTag>();
    public ICollection<VenuePhoto> Photos { get; set; } = new List<VenuePhoto>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<CultureEvent> Events { get; set; } = new List<CultureEvent>();
}
