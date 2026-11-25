namespace Nereye.Domain.Entities;

/// <summary>Bildirim almak isteyen cihaz (açık rıza). Hesap silinince ya da izin geri alınınca silinir.</summary>
public sealed class PushDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Token { get; set; }
    public required string Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

/// <summary>Gönderilmiş bildirim: aynı etkinlik için ikinci kez gönderilmesin, günlük sınır sayılsın.</summary>
public sealed class PushLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public required string Key { get; set; }
    public DateTime SentAt { get; set; }
}
