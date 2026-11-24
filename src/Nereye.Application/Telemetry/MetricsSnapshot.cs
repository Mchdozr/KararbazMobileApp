namespace Nereye.Application.Telemetry;

/// <summary>Uç başına istek sayısı ve gecikme yüzdelikleri (son pencere).</summary>
public sealed record RouteStat(string Route, long Count, long ServerErrors, double? P50Ms, double? P95Ms, double? P99Ms);

/// <summary>Dış servis (Overpass, Google, OSRM, Nominatim…) çağrıları.</summary>
public sealed record UpstreamStat(string Service, long Count, long Errors, long Limited, double? P50Ms, double? P95Ms);

/// <summary>Önbellek katmanı: bayat kayıt da kullanıcıya hemen sunulduğu için isabet sayılır.</summary>
public sealed record CacheStat(string Layer, long Lookups, long Hits, long Stale, long Misses, double? HitRatio);

public sealed record MetricsSnapshot(
    DateTime CapturedAt,
    string Instance,
    int WindowMinutes,
    IReadOnlyList<RouteStat> Routes,
    IReadOnlyList<UpstreamStat> Upstreams,
    IReadOnlyList<CacheStat> Caches);

/// <summary>API'nin yayımladığı son ölçüm özeti (yönetim paneli ayrı süreçtir; paylaşılan önbellekten okur).</summary>
public interface IMetricsReader
{
    Task<MetricsSnapshot?> LatestAsync(CancellationToken cancellationToken = default);
}
