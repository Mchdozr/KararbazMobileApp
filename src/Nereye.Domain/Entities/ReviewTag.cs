namespace Nereye.Domain.Entities;

public sealed class ReviewTag
{
    public Guid ReviewId { get; set; }
    public VibeTag Tag { get; set; }
}
