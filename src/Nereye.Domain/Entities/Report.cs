namespace Nereye.Domain.Entities;

public sealed class Report
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid? VenueId { get; set; }
    public Guid? ReviewId { get; set; }

    /// <summary>Bildirilen yorum fotoğrafı (yalnızca bu API'nin yükleme yolu).</summary>
    public string? PhotoUrl { get; set; }
    public required string Reason { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
}
