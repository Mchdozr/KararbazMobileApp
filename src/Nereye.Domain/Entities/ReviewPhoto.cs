namespace Nereye.Domain.Entities;

public sealed class ReviewPhoto
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public required string Url { get; set; }
}
