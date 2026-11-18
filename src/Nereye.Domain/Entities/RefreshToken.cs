namespace Nereye.Domain.Entities;

/// <summary>
/// Yenileme token'ı (yalnızca özeti saklanır). Kullanılan token silinmez, iptal edilmiş olarak süresi dolana dek kalır:
/// aynı token tekrar gelirse çalınmış sayılır ve kullanıcının tüm oturumları kapatılır.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Eşzamanlılık belirteci: aynı token'ı iki istek birden yenileyemez.</summary>
    public bool Revoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Yenilemeyle iptal edildiyse yerine verilen token. Çıkış ya da toplu iptalde boştur.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
