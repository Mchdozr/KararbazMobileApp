namespace Nereye.Domain.Entities;

/// <summary>
/// Uygulama ya da sunucu hatası, gün ve parmak izi başına tek satır (Count artar). Kullanıcı kimliği tutulmaz;
/// metinler kişisel veriden ayıklanmış olarak yazılır (ErrorScrubber). 30 gün saklanır.
/// </summary>
public sealed class ErrorReport
{
    public Guid Id { get; set; }
    public DateTime Day { get; set; }
    public required string Fingerprint { get; set; }
    /// <summary>"app" ya da "server".</summary>
    public required string Source { get; set; }
    public required string Platform { get; set; }
    public string? AppVersion { get; set; }
    public string? Screen { get; set; }
    public required string Message { get; set; }
    public string? Stack { get; set; }
    public bool Fatal { get; set; }
    public int Count { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
