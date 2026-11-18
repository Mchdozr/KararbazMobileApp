namespace Nereye.Domain.Entities;

/// <summary>Bir kullanıcının başka bir kullanıcıyı engellemesi: engellenenin içeriği engelleyene gösterilmez.</summary>
public sealed class UserBlock
{
    public Guid BlockerUserId { get; set; }
    public Guid BlockedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
