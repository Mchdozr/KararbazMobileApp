using Microsoft.EntityFrameworkCore;
using Nereye.Application.Places;
using Nereye.Application.Recommendations;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;
using Nereye.Domain.Promotion;
using Nereye.Domain.Recommendation;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Recommendations;

public sealed class RecommendationService : IRecommendationService
{
    private readonly IPlacesClient _places;
    private readonly IPlacesCache _cache;
    private readonly NereyeDbContext _db;
    private readonly Application.Common.IClock _clock;
    private readonly Places.PlaceCovers _covers;

    public RecommendationService(
        IPlacesClient places,
        IPlacesCache cache,
        NereyeDbContext db,
        Application.Common.IClock clock,
        Places.PlaceCovers covers)
    {
        _covers = covers;
        _places = places;
        _cache = cache;
        _db = db;
        _clock = clock;
    }

    public async Task<RecommendationResponse> RecommendAsync(RecommendationQuery query, CancellationToken cancellationToken = default)
    {
        var geohash = Geohash.Encode(query.Latitude, query.Longitude, 7);
        var snapshots = await LoadSnapshotsAsync(query, geohash, cancellationToken);

        var placeIds = snapshots.Select(s => s.PlaceId).ToList();
        var overlays = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Include(v => v.Events)
            .Where(v => placeIds.Contains(v.GooglePlaceId))
            .ToListAsync(cancellationToken);
        var overlayByPlace = overlays.ToDictionary(v => v.GooglePlaceId);
        var overlayIds = overlays.Select(o => o.Id).ToList();
        var sinceTrend = _clock.UtcNow.AddDays(-21);
        var saveCounts = overlayIds.Count == 0
            ? new Dictionary<Guid, int>()
            : (await _db.Favorites
                .Where(f => overlayIds.Contains(f.VenueId) && f.CreatedAt >= sinceTrend)
                .Select(f => f.VenueId)
                .ToListAsync(cancellationToken))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        IReadOnlyList<VibeTag> userVibes = query.Vibes;
        HashSet<string> recentFavorites = [];
        if (query.UserId is { } uid)
        {
            var pref = await _db.UserPreferences.AsNoTracking().Include(p => p.Vibes)
                .FirstOrDefaultAsync(p => p.UserId == uid, cancellationToken);
            if (userVibes.Count == 0 && pref is not null)
            {
                userVibes = pref.Vibes.Select(v => v.Tag).ToList();
            }

            var since = _clock.UtcNow.AddDays(-21);
            recentFavorites = (await _db.Favorites
                    .Where(f => f.UserId == uid && f.CreatedAt >= since)
                    .Select(f => f.VenueId)
                    .ToListAsync(cancellationToken))
                .Select(id => overlays.FirstOrDefault(v => v.Id == id)?.GooglePlaceId)
                .Where(id => id is not null)
                .Cast<string>()
                .ToHashSet();
        }

        var organic = new List<RankedCard>();
        var scored = new Dictionary<string, (PlaceSnapshot Place, Venue? Overlay, double Score, double Distance, OpenState Open)>();
        var now = Places.ServeTime.Now(_clock);

        foreach (var cachedPlace in snapshots)
        {
            // Önbellekteki "açık" bayrağı çekildiği ana ait; saat metninden şimdi (İstanbul) yeniden hesaplanır.
            var (place, openState) = OpenNowResolver.Apply(cachedPlace, now);
            overlayByPlace.TryGetValue(place.PlaceId, out var overlay);
            var distance = GeoMath.HaversineMeters(query.Latitude, query.Longitude, place.Latitude, place.Longitude);
            var open = place.OpenNow switch
            {
                true => OpenStatus.Open,
                false => OpenStatus.Closed,
                _ => OpenStatus.Unknown
            };

            if (query.OpenNowOnly && open == OpenStatus.Closed)
            {
                continue;
            }

            if (!IntentPlaceTypes.Fits(query.Intent, place.Name, place.Cuisine, place.Types))
            {
                continue;
            }

            var venueVibes = overlay?.Tags.Select(t => t.Tag).ToList() ?? [];
            var recentSaves = overlay is null ? 0 : saveCounts.GetValueOrDefault(overlay.Id);
            var score = RecommendationScorer.Score(new ScoreInput(
                distance,
                overlay?.NereyeRatingAvg,
                overlay?.RatingCount ?? 0,
                IntentMatches(place, query.Intent),
                open,
                VibeMatcher.Score(userVibes, venueVibes),
                recentSaves,
                recentFavorites.Contains(place.PlaceId),
                query.Intent));

            organic.Add(new RankedCard(place.PlaceId, score, CardKind.Organic));
            scored[place.PlaceId] = (place, overlay, score, distance, openState);
        }

        var rankedOrganic = organic.OrderByDescending(c => c.Score).Take(PromotionSlotAssembler.MaxOrganic).ToList();

        var sponsored = await LoadSponsoredAsync(query.Intent, geohash, cancellationToken);
        var assembled = PromotionSlotAssembler.Assemble(rankedOrganic, sponsored);
        var (page, hasMore) = RecommendationPage.Slice(assembled, query.Skip, query.Take);

        var items = page.Select(card =>
        {
            if (!scored.TryGetValue(card.PlaceId, out var row))
            {
                return null;
            }

            var place = row.Place;
            var overlay = row.Overlay;
            var score = row.Score;
            var distance = row.Distance;
            var events = MapEvents(overlay, query.Intent);
            return new RecommendationItemDto(
                place.PlaceId,
                overlay?.OwnerDisplayName ?? place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                Math.Round(distance, 1),
                Math.Round(card.Kind == CardKind.Sponsored ? score : card.Score, 4),
                card.Kind,
                place.OpenNow,
                overlay?.NereyeRatingAvg,
                overlay?.RatingCount ?? 0,
                place.PhotoReference,
                PlaceMedia.Url(overlay?.Photos.FirstOrDefault()?.Url, place.PhotoReference),
                overlay?.Tags.Select(t => t.Tag).ToList() ?? [],
                events,
                PlaceBlurb.Compose(place, distance, overlay?.Tags.Select(t => t.Tag).ToList()),
                place.GoogleRating,
                place.GoogleUserRatingCount,
                place.PriceLevel,
                place.Category,
                row.Open.ClosesAt,
                row.Open.ClosesInMinutes,
                row.Open.OpensAt,
                row.Open.OpensInMinutes);
        }).Where(i => i is not null).Cast<RecommendationItemDto>().ToList();

        // Görseli olmayan kartlara daha önce bulunmuş gerçek kapak: istemci stok görsel göstermez, ek istek atmaz.
        items = await _covers.FillAsync(items, cancellationToken);
        var attribution = snapshots.FirstOrDefault()?.Attribution ?? "Harita verisi: OpenStreetMap";
        return new RecommendationResponse(items, geohash, attribution, hasMore);
    }

    private async Task<IReadOnlyList<PlaceSnapshot>> LoadSnapshotsAsync(
        RecommendationQuery query,
        string geohash,
        CancellationToken cancellationToken)
    {
        List<PlaceSnapshot>? snapshots = null;
        foreach (var bucket in PlacesCacheKey.ServingBuckets(query.RadiusMeters))
        {
            var key = PlacesCacheKey.Nearby(geohash, query.Intent, bucket);
            snapshots = await _cache.TryGetAsync<List<PlaceSnapshot>>(key, cancellationToken);
            if (snapshots is not null)
            {
                break;
            }
        }

        if (snapshots is null)
        {
            var bucket = PlacesCacheKey.BucketMeters(query.RadiusMeters);
            var key = PlacesCacheKey.Nearby(geohash, query.Intent, bucket);
            var fresh = false;
            snapshots = await _cache.GetOrSetAsync(
                key,
                PlacesCacheTtl.Nearby,
                async ct =>
                {
                    fresh = true;
                    var found = await _places.NearbyAsync(query.Latitude, query.Longitude, query.Intent, bucket, ct);
                    return found.ToList();
                },
                cancellationToken);

            // Detay önbelleğini yalnızca liste gerçekten yeni çekildiğinde ve hepsini aynı anda doldur.
            // (Önceden her öneri isteğinde, önbellekten gelse bile, mekan başına sırayla bir yazma yapılıyordu.)
            if (fresh)
            {
                await Task.WhenAll(snapshots.Select(place =>
                    _cache.SetAsync(PlacesCacheKey.Details(place.PlaceId), PlacesCacheTtl.Nearby, place, cancellationToken)));
            }
        }

        return NearbyPlaces.WithinRadius(snapshots, query.Latitude, query.Longitude, query.RadiusMeters);
    }

    private async Task<IReadOnlyList<RankedCard>> LoadSponsoredAsync(Intent intent, string geohash, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var stem = geohash.Length >= 6 ? geohash[..6] : geohash;
        var active = await _db.PromotionCampaigns
            .Where(c => c.Status == CampaignStatus.Active
                        && c.TargetIntent == intent
                        && c.StartAt <= now
                        && c.EndAt >= now
                        && (c.TargetGeohash == geohash
                            || c.TargetGeohash.StartsWith(stem)
                            || geohash.StartsWith(c.TargetGeohash)))
            .Join(_db.Venues, c => c.VenueId, v => v.Id, (c, v) => new { c.Id, v.GooglePlaceId })
            .ToListAsync(cancellationToken);

        foreach (var row in active)
        {
            _db.PromotionImpressions.Add(new PromotionImpression
            {
                Id = Guid.NewGuid(),
                CampaignId = row.Id,
                CreatedAt = now
            });
        }

        if (active.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return active.Select(row => new RankedCard(row.GooglePlaceId, 0, CardKind.Sponsored)).DistinctBy(c => c.PlaceId).ToList();
    }

    private List<CultureEventDto> MapEvents(Venue? overlay, Intent intent)
    {
        if (overlay is null || intent != Intent.Kultur)
        {
            return [];
        }

        var now = _clock.UtcNow;
        return overlay.Events
            .Where(e => e.StartsAt >= now.AddHours(-2) && e.StartsAt <= now.AddDays(14))
            .OrderBy(e => e.StartsAt)
            .Take(3)
            .Select(e => new CultureEventDto(e.Id, e.Title, e.StartsAt, e.EndsAt))
            .ToList();
    }

    private static bool IntentMatches(PlaceSnapshot place, Intent intent) =>
        IntentPlaceTypes.Fits(intent, place.Name, place.Cuisine, place.Types);
}
