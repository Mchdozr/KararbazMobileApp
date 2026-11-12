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
    private const double MatchMeters = 90;
    private readonly IPlacesClient _osm;
    private readonly IPlacesClient _google;
    private readonly GooglePlacesOptions _options;
    private readonly IPlacesCache _cache;
    private readonly ILogger<GoogleAugmentedPlacesClient>? _log;

    internal TimeSpan GoogleBudget { get; init; } = TimeSpan.FromMilliseconds(1200);

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
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            googleTask = GoogleNearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
        }

        var osm = await _osm.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
        if (googleTask is null)
        {
            return osm;
        }

        try
        {
            var google = await googleTask.WaitAsync(GoogleBudget, cancellationToken);
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

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        _osm.TextSearchAsync(query, latitude, longitude, cancellationToken);

    public async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default)
    {
        if (placeId.StartsWith("g:", StringComparison.Ordinal))
        {
            return await _google.DetailsAsync(placeId[2..], cancellationToken);
        }

        var osm = await _osm.DetailsAsync(placeId, cancellationToken);
        if (osm is not null || string.IsNullOrWhiteSpace(_options.ApiKey) || placeId.StartsWith("osm:", StringComparison.OrdinalIgnoreCase))
        {
            return osm;
        }

        return await _google.DetailsAsync(placeId, cancellationToken);
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
            var hit = google.FirstOrDefault(g => !used.Contains(g.PlaceId) && Same(place, g));
            if (hit is null)
            {
                merged.Add(place);
                continue;
            }

            used.Add(hit.PlaceId);
            merged.Add(place with
            {
                Phone = string.IsNullOrWhiteSpace(place.Phone) ? hit.Phone : place.Phone,
                Website = string.IsNullOrWhiteSpace(place.Website) ? hit.Website : place.Website,
                OpeningHoursText = string.IsNullOrWhiteSpace(place.OpeningHoursText) ? hit.OpeningHoursText : place.OpeningHoursText,
                PhotoReference = string.IsNullOrWhiteSpace(place.PhotoReference) ? hit.PhotoReference : place.PhotoReference,
                OpenNow = hit.OpenNow ?? place.OpenNow,
                GoogleRating = hit.GoogleRating ?? place.GoogleRating,
                GoogleUserRatingCount = hit.GoogleUserRatingCount ?? place.GoogleUserRatingCount,
                Attribution = "Harita verisi: OpenStreetMap · Google"
            });
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

    private static bool Same(PlaceSnapshot osm, PlaceSnapshot google)
    {
        var meters = GeoMath.HaversineMeters(osm.Latitude, osm.Longitude, google.Latitude, google.Longitude);
        if (meters > MatchMeters)
        {
            return false;
        }

        var a = Norm(osm.Name);
        var b = Norm(google.Name);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        return a == b || (a.Length >= 4 && b.Contains(a, StringComparison.Ordinal)) || (b.Length >= 4 && a.Contains(b, StringComparison.Ordinal));
    }

    private static string Norm(string name)
    {
        var lower = name.ToLowerInvariant()
            .Replace('ı', 'i').Replace('İ', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
        var chars = lower.Where(char.IsLetterOrDigit).ToArray();
        var flat = new string(chars);
        foreach (var drop in new[] { "coffeeshop", "coffee", "kahve", "cafe", "kafe", "restaurant", "restoran" })
        {
            flat = flat.Replace(drop, "", StringComparison.Ordinal);
        }

        return flat;
    }
}
