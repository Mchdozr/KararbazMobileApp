using Nereye.Domain.Analytics;

namespace Nereye.Application.Analytics;

public sealed record AnalyticsEvent(string? Name, string? PlaceId, string? Intent);

public sealed record AnalyticsBatch(IReadOnlyList<AnalyticsEvent>? Events);

public sealed record VenueStatsDay(DateTime Day, long Views, long Directions, long Saves);

/// <summary>
/// İşletme istatistiği. Görüntülenme ve yol tarifi yalnızca analitiğe izin veren kullanıcılardan sayılır;
/// kaydetme gerçek kayıtlardan (tüm kullanıcılar, yalnızca toplam).
/// </summary>
public sealed record VenueStatsDto(long Views, long Directions, long Saves, long Shares, IReadOnlyList<VenueStatsDay> Days);

public sealed record CountItem(string Key, long Count);

public sealed record FunnelDto(
    IReadOnlyList<FunnelStep> Steps,
    IReadOnlyList<CountItem> Intents,
    IReadOnlyList<CountItem> TopVenues,
    IReadOnlyDictionary<string, long> Others);

public interface IAnalyticsService
{
    Task RecordAsync(IReadOnlyList<AnalyticsEvent> events, CancellationToken cancellationToken = default);

    Task<VenueStatsDto> CountsAsync(string placeId, int days, CancellationToken cancellationToken = default);

    /// <summary>Yalnızca mekanın onaylı sahibi görür.</summary>
    Task<VenueStatsDto> OwnerStatsAsync(string placeId, Guid ownerId, int days, CancellationToken cancellationToken = default);

    Task<FunnelDto> FunnelAsync(int days, CancellationToken cancellationToken = default);
}
