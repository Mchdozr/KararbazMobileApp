namespace Nereye.Domain.Places;

/// <summary>
/// "Bayatken sun, arkada tazele" kuralı. Her önbellek kaydının yanında çekildiği an (damga) tutulur;
/// yumuşak süre dolunca kayıt hâlâ sunulur ama arka planda yenisi çekilir. Kesin süre (TTL) değişmez.
/// </summary>
public static class CacheFreshness
{
    /// <summary>Damga yoksa (eski sürümden kalan kayıt) bayat sayılır; gelecekteki damga taze sayılır.</summary>
    public static bool IsStale(DateTime? fetchedAtUtc, DateTime nowUtc, TimeSpan softTtl) =>
        fetchedAtUtc is not { } at || nowUtc - at >= softTtl;

    /// <summary>
    /// Tazeleme başarısız olduğunda yazılan damga: kayıt <paramref name="retryAfter"/> sonra yeniden bayat olur.
    /// Böylece dış servis çökükken her istek yeni bir deneme başlatmaz.
    /// </summary>
    public static DateTime RetryStamp(DateTime nowUtc, TimeSpan softTtl, TimeSpan retryAfter) =>
        nowUtc - softTtl + retryAfter;
}
