namespace Nereye.Domain.Entities;

public sealed class VenuePhoto
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public required string Url { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
