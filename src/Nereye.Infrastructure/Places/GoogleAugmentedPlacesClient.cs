using Microsoft.Extensions.Logging;
using Nereye.Application.Places;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Katalog OpenStreetMap'tir. Google Places anahtarı varsa aynı dairedeki güncel
/// telefon, saat, puan ve fotoğraf OSM kaydının üstüne yazılır; OSM'de olmayanlar eklenir.
/// </summary>
public sealed class GoogleAugmentedPlacesClient : IPlacesClient
{
    private readonly IPlacesClient _osm;
    private readonly IPlacesClient _google;
    private readonly GooglePlacesOptions _options;
    private readonly IPlacesCache _cache;
    private readonly ILogger<GoogleAugmentedPlacesClient>? _log;

    internal TimeSpan GoogleBudget { get; init; } = TimeSpan.FromMilliseconds(1200);

    internal TimeSpan RichBudget { get; init; } = TimeSpan.FromMilliseconds(2500);

    public GoogleAugmentedPlacesClient(
        IPlacesClient osm,
        IPlacesClient google,
        GooglePlacesOptions options,
        IPlacesCache cache,
        ILogger<GoogleAugmentedPlacesClient>? log = null)
    {
        _osm = osm;
        _google = google;
        _options = options;
        _cache = cache;
        _log = log;
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<PlaceSnapshot>?>? googleTask = null;
        if (!string.IsNullOrWhiteSpace(_options.ApiKey) && (_google as GooglePlacesClient)?.CanSearchNearby != false)
        {
            googleTask = GoogleNearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
        }

        IReadOnlyList<PlaceSnapshot> osm;
        try
        {
            osm = await _osm.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
        }
        catch (Exception ex) when (
            googleTask is not null
            && (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)))
        {
            // OSM tamamen kapalı: Google varsa liste onunla dolar, kullanıcı hata görmez.
            _log?.LogWarning("OSM nearby failed ({Reason}); serving Google only", ex.Message);
            osm = [];
        }

        if (googleTask is null)
        {
            return osm;
        }

        try
        {
            // Elde OSM sonucu varsa Google'ı kısa bir süre bekle; yoksa gösterecek başka şey olmadığı için sonuna kadar bekle.
            var google = osm.Count == 0
                ? await googleTask.WaitAsync(cancellationToken)
                : await googleTask.WaitAsync(GoogleBudget, cancellationToken);
            return google is null ? osm : Merge(osm, google);
        }
        catch (TimeoutException)
        {
            PersistLate(googleTask, osm, latitude, longitude, intent, radiusMeters);
            return osm;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Google Places nearby failed; keeping OSM results");
            return osm;
        }
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlaceSnapshot> osm = [];
        try
        {
            osm = await _osm.TextSearchAsync(query, latitude, longitude, cancellationToken);
        }
        catch (Exception ex) when (
            !string.IsNullOrWhiteSpace(_options.ApiKey)
            && (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)))
        {
            _log?.LogWarning("OSM text search failed ({Reason}); trying Google", ex.Message);
        }

        if (osm.Count > 0 || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return osm;
        }

        // OSM'de karşılığı olmayan aramalar ("kahvaltı", "canlı müzik") Google ile yanıtlanır.
        try
        {
            var google = await _google.TextSearchAsync(query, latitude, longitude, cancellationToken);
            return google.Select(g => g with { PlaceId = "g:" + g.PlaceId }).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning("Google text search failed: {Reason}", ex.Message);
            return osm;
        }
    }

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        if (placeId.StartsWith("g:", StringComparison.Ordinal))
        {
            var direct = await _google.DetailsAsync(placeId[2..], cancellationToken);
            return direct is null ? null : direct with { PlaceId = placeId };
        }

        var osm = await _osm.DetailsAsync(placeId, cancellationToken);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return osm;
        }

        if (osm is null)
        {
            return placeId.StartsWith("osm:", StringComparison.OrdinalIgnoreCase)
                ? null
                : await _google.DetailsAsync(placeId, cancellationToken);
        }

        return await EnrichAsync(osm, cancellationToken);
    }

    /// <summary>
    /// OSM kaydına Google'daki karşılığının özetini, yorumlarını, olanaklarını ve galerisini ekler.
    /// Google yavaş ya da kapalıysa OSM kaydı olduğu gibi döner: detay sayfası asla Google'ı beklemez.
    /// </summary>
    private async Task<PlaceSnapshot> EnrichAsync(PlaceSnapshot osm, CancellationToken cancellationToken)
    {
        if (_google is not IRichPlaceSource rich || _google is GooglePlacesClient { CanEnrich: false })
        {
            return osm;
        }

        // Google'da karşılığı olmayan mekanı her açılışta yeniden aramak kotayı boşa harcar.
        var missKey = "places:richmiss:v2:" + osm.PlaceId;
        if (await _cache.TryGetAsync<string>(missKey, cancellationToken) is not null)
        {
            return osm;
        }

        try
        {
            var hit = await rich.FindRichAsync(osm, cancellationToken).WaitAsync(RichBudget, cancellationToken);
            if (hit is null)
            {
                // Kota bu çağrı sırasında dolduysa "bulunamadı" değil "sorulamadı"dır; kalıcı kaydetme.
                if (_google is not GooglePlacesClient { CanEnrich: false })
                {
                    await _cache.SetAsync(missKey, PlacesCacheTtl.Rich, "1", cancellationToken);
                }

                return osm;
            }

            return PlaceMerge.Overlay(osm, hit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log?.LogWarning("Google rich details skipped for {PlaceId}: {Reason}", osm.PlaceId, ex.Message);
            return osm;
        }
    }

    internal static IReadOnlyList<PlaceSnapshot> Merge(
        IReadOnlyList<PlaceSnapshot> osm,
        IReadOnlyList<PlaceSnapshot> google)
    {
        if (google.Count == 0)
        {
            return osm;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<PlaceSnapshot>(osm.Count + google.Count);
        foreach (var place in osm)
        {
            var hit = google.FirstOrDefault(g => !used.Contains(g.PlaceId) && PlaceMerge.Same(place, g));
            if (hit is null)
            {
                merged.Add(place);
                continue;
            }

            used.Add(hit.PlaceId);
            merged.Add(PlaceMerge.Overlay(place, hit));
        }

        foreach (var extra in google)
        {
            if (used.Contains(extra.PlaceId))
            {
                continue;
            }

            merged.Add(extra with { PlaceId = "g:" + extra.PlaceId });
        }

        return merged;
    }

    private async Task<IReadOnlyList<PlaceSnapshot>?> GoogleNearbyAsync(
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _google.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Google Places nearby failed; keeping OSM results");
            return null;
        }
    }

    private void PersistLate(
        Task<IReadOnlyList<PlaceSnapshot>?> googleTask,
        IReadOnlyList<PlaceSnapshot> osm,
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters)
    {
        _ = PersistWhenReadyAsync(googleTask, osm, latitude, longitude, intent, radiusMeters);
    }

    private async Task PersistWhenReadyAsync(
        Task<IReadOnlyList<PlaceSnapshot>?> googleTask,
        IReadOnlyList<PlaceSnapshot> osm,
        double latitude,
        double longitude,
        Intent intent,
        int radiusMeters)
    {
        try
        {
            var google = await googleTask.ConfigureAwait(false);
            if (google is null || google.Count == 0)
            {
                return;
            }

            var key = PlacesCacheKey.Nearby(Geohash.Encode(latitude, longitude, 6), intent, radiusMeters);
            await _cache.SetAsync(key, PlacesCacheTtl.Nearby, Merge(osm, google).ToList()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Late Google merge was not cached");
        }
    }
}
