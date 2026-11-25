namespace Nereye.Domain.Entities;

/// <summary>Gün x olay x anahtar (mekan kimliği ya da niyet) sayacı. Kişiye ait hiçbir alan yoktur.</summary>
public sealed class DailyCount
{
    public Guid Id { get; set; }
    public DateTime Day { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public long Count { get; set; }
}
