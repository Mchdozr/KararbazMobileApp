namespace Nereye.Domain.Entities;

/// <summary>Kullanıcının adlandırılmış mekan listesi. ShareCode doluysa bağlantıyla herkes okuyabilir.</summary>
public sealed class VenueList
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? ShareCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<VenueListItem> Items { get; set; } = [];
}

/// <summary>Listedeki mekan: kart için gereken bilgiler saklanır (mekan her zaman yerel veritabanında değildir).</summary>
public sealed class VenueListItem
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public required string PlaceId { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public string? PhotoUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime AddedAt { get; set; }
}
