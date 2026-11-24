using Microsoft.EntityFrameworkCore;
using Nereye.Application.Places;
using Nereye.Application.Recommendations;
using Nereye.Application.Venues;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;
using Nereye.Domain.Recommendation;
using Nereye.Infrastructure.Persistence;
using Nereye.Infrastructure.Places;
using VenueCulture = Nereye.Application.Venues.CultureEventDto;

namespace Nereye.Infrastructure.Venues;

public sealed class VenueQueryService : IVenueQueryService
{
    private static readonly TimeSpan DetailsTtl = PlacesCacheTtl.Nearby;
    private readonly IPlacesClient _places;
    private readonly IPlacesCache _cache;
    private readonly IPlacePhotoEnricher _photos;
    private readonly NereyeDbContext _db;
    private readonly AvatarStorage _uploads;
    private readonly PlaceCovers _covers;
    private readonly Application.Common.IClock _clock;

    public VenueQueryService(
        IPlacesClient places,
        IPlacesCache cache,
        IPlacePhotoEnricher photos,
        NereyeDbContext db,
        AvatarStorage uploads,
        PlaceCovers covers,
        Application.Common.IClock clock)
    {
        _clock = clock;
        _covers = covers;
        _places = places;
        _cache = cache;
        _photos = photos;
        _db = db;
        _uploads = uploads;
    }

    public async Task<VenueDetailDto?> GetAsync(string placeId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        // Dış servis (yavaş olabilir) ile veritabanı sorgusu birbirini beklemesin.
        var placeTask = LoadRichAsync(placeId, cancellationToken);
        var overlay = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Include(v => v.Events)
            .FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);

        var saved = false;
        if (userId is { } uid && overlay is not null)
        {
            saved = await _db.Favorites.AnyAsync(f => f.UserId == uid && f.VenueId == overlay.Id, cancellationToken);
        }

        var place = await placeTask;
        if (place is null)
        {
            return null;
        }

        var gallery = (place.Photos ?? [])
            .Select(p => PlaceMedia.Url(null, p))
            .Append(PlaceMedia.Url(null, place.PhotoReference))
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (gallery.Count > 0)
        {
            // Detayda bulunan gerçek kare, listelerde bu mekanın kapağı olur (stok görsel yerine).
            await _covers.RememberAsync(placeId, gallery[0], cancellationToken);
        }

        var (_, open) = OpenNowResolver.Apply(place, ServeTime.Now(_clock));
        var google = new GoogleVenueBlock(
            place.Name,
            place.Address,
            place.Latitude,
            place.Longitude,
            open.IsOpen,
            place.GoogleRating,
            place.GoogleUserRatingCount,
            place.PhotoReference,
            place.GoogleMapsUri,
            place.Attribution,
            place.Types,
            place.Phone,
            place.Website,
            place.OpeningHoursText,
            place.Cuisine,
            place.OutdoorSeating,
            place.InternetAccess,
            place.Summary,
            place.PriceLevel,
            place.Category,
            gallery,
            place.Features,
            open.ClosesAt,
            open.ClosesInMinutes,
            open.OpensAt,
            open.OpensInMinutes);

        var nereye = new NereyeVenueBlock(
            overlay?.Id,
            overlay?.NereyeRatingAvg,
            overlay?.RatingCount ?? 0,
            overlay?.Claimed ?? false,
            overlay?.OwnerDisplayName,
            overlay?.OwnerDescription,
            overlay?.Tags.Select(t => t.Tag).ToList() ?? [],
            overlay?.Photos.Select(p => p.Url).ToList() ?? [],
            overlay?.Geohash ?? Geohash.Encode(place.Latitude, place.Longitude),
            saved);

        var events = overlay?.Events
            .OrderBy(e => e.StartsAt)
            .Select(e => new VenueCulture(e.Id, e.Title, e.StartsAt, e.EndsAt))
            .ToList() ?? [];

        var note = PlaceBlurb.Compose(place, 0, overlay?.Tags.Select(t => t.Tag).ToList());
        var external = place.Reviews?
            .Select(r => new ExternalReviewDto(r.Author, r.Rating, r.Text, r.When, r.AuthorPhotoUrl, r.AuthorUri))
            .ToList();
        return new VenueDetailDto(placeId, google, nereye, events, note, PlaceAmenityChips.From(place), external);
    }

    /// <summary>
    /// Detay sayfası için zengin kayıt. Sağlayıcı yanıt vermezse listeden gelen sade kayda düşer:
    /// sayfa eksik içerikle de olsa açılır.
    /// </summary>
    private async Task<PlaceSnapshot?> LoadRichAsync(string placeId, CancellationToken cancellationToken)
    {
        try
        {
            var rich = await _cache.GetOrSetAsync(
                PlacesCacheKey.Rich(placeId),
                PlacesCacheTtl.Rich,
                // Zengin (Atmosphere) alanlar yalnızca burada, tek mekan detayında istenir.
                ct => _places is IRichPlaceDetails rich ? rich.RichDetailsAsync(placeId, ct) : _places.DetailsAsync(placeId, ct),
                cancellationToken);
            if (rich is not null)
            {
                return rich;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            /* aşağıda sade kayda düş */
        }

        return await _cache.TryGetAsync<PlaceSnapshot>(PlacesCacheKey.Details(placeId), cancellationToken);
    }

    public async Task UpdateOwnerAsync(
        string placeId,
        Guid userId,
        string? displayName,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var venue = await RequireOwnerVenue(placeId, userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            venue.OwnerDisplayName = displayName.Trim();
        }

        if (description is not null)
        {
            venue.OwnerDescription = description.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> AddOwnerPhotoAsync(
        string placeId,
        Guid userId,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        var venue = await RequireOwnerVenue(placeId, userId, cancellationToken);
        var url = _uploads.SaveVenue(bytes);
        _db.VenuePhotos.Add(new VenuePhoto
        {
            Id = Guid.NewGuid(),
            VenueId = venue.Id,
            Url = url,
            CreatedAt = DateTime.UtcNow,
            UploadedByUserId = userId
        });
        await _db.SaveChangesAsync(cancellationToken);
        return url;
    }

    private async Task<Venue> RequireOwnerVenue(string placeId, Guid userId, CancellationToken cancellationToken)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken)
                    ?? throw new InvalidOperationException("Mekan yok.");
        if (venue.OwnerUserId != userId)
        {
            throw new UnauthorizedAccessException("Sadece işletme güncelleyebilir.");
        }

        return venue;
    }

    public async Task<string?> GetPhotoAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var overlay = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Photos)
            .FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        var owner = overlay?.Photos.Select(p => p.Url).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (!string.IsNullOrWhiteSpace(owner))
        {
            return PlaceMedia.Url(owner, null);
        }

        if (await _covers.GetAsync(placeId, cancellationToken) is { } cover)
        {
            return cover;
        }

        var place = await _cache.GetOrSetAsync(
            PlacesCacheKey.Details(placeId),
            DetailsTtl,
            ct => _places.DetailsAsync(placeId, ct),
            cancellationToken);
        if (place is null)
        {
            return null;
        }

        var enriched = await _photos.EnrichOneAsync(place, cancellationToken);
        var url = PlaceMedia.Url(null, enriched.PhotoReference);
        await _covers.RememberAsync(placeId, url, cancellationToken);
        return url;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPhotosAsync(
        IReadOnlyList<string> placeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = PhotoBatch.Take(placeIds);
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var overlays = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Photos)
            .Where(v => ids.Contains(v.GooglePlaceId))
            .ToListAsync(cancellationToken);
        var overlayById = overlays.ToDictionary(v => v.GooglePlaceId);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var lookups = new List<Task<PlaceSnapshot?>>();
        var covers = await _covers.GetManyAsync(ids, cancellationToken);
        foreach (var id in ids)
        {
            overlayById.TryGetValue(id, out var overlay);
            var owner = overlay?.Photos.Select(p => p.Url).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            var owned = PlaceMedia.Url(owner, null);
            if (!string.IsNullOrWhiteSpace(owned))
            {
                found[id] = owned;
                continue;
            }

            if (covers.TryGetValue(id, out var cover))
            {
                found[id] = cover;
                continue;
            }

            // Detay daha önce açıldıysa zengin kaydın galerisi elde: kotasız, anında.
            var rich = await _cache.TryGetAsync<PlaceSnapshot>(PlacesCacheKey.Rich(id), cancellationToken);
            var fromRich = rich is null ? null : PlaceMedia.Url(null, rich.PhotoReference ?? rich.Photos?.FirstOrDefault());
            if (!string.IsNullOrWhiteSpace(fromRich))
            {
                found[id] = fromRich;
                await _covers.RememberAsync(id, fromRich, cancellationToken);
                continue;
            }

            lookups.Add(_cache.GetOrSetAsync(
                PlacesCacheKey.Details(id),
                DetailsTtl,
                ct => _places.DetailsAsync(id, ct),
                cancellationToken));
        }

        // Sekiz mekanı sırayla değil aynı anda sor.
        var missing = new List<PlaceSnapshot>();
        foreach (var place in await Task.WhenAll(lookups))
        {
            if (place is null)
            {
                continue;
            }

            var tagged = PlaceMedia.Url(null, place.PhotoReference);
            if (!string.IsNullOrWhiteSpace(tagged))
            {
                found[place.PlaceId] = tagged;
                await _covers.RememberAsync(place.PlaceId, tagged, cancellationToken);
                continue;
            }

            missing.Add(place);
        }

        if (missing.Count == 0)
        {
            return found;
        }

        var enriched = await _photos.EnrichLiteAsync(missing, cancellationToken);
        foreach (var place in enriched)
        {
            var url = PlaceMedia.Url(null, place.PhotoReference);
            if (!string.IsNullOrWhiteSpace(url))
            {
                found[place.PlaceId] = url;
                await _covers.RememberAsync(place.PlaceId, url, cancellationToken);
            }
        }

        return found;
    }

    public async Task<IReadOnlyList<RecommendationItemDto>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var geohash = Geohash.Encode(latitude, longitude);
        var snapshots = await _cache.GetOrSetAsync(
            PlacesCacheKey.TextSearch(geohash, query),
            TimeSpan.FromMinutes(15),
            async ct =>
            {
                return await _places.TextSearchAsync(query, latitude, longitude, ct);
            },
            cancellationToken);

        var searchIds = snapshots.Select(s => s.PlaceId).ToList();
        var overlays = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Where(v => searchIds.Contains(v.GooglePlaceId))
            .ToListAsync(cancellationToken);
        var map = overlays.ToDictionary(v => v.GooglePlaceId);

        var now = ServeTime.Now(_clock);
        var items = snapshots.Select(cached =>
        {
            var (p, open) = OpenNowResolver.Apply(cached, now);
            map.TryGetValue(p.PlaceId, out var overlay);
            var distance = GeoMath.HaversineMeters(latitude, longitude, p.Latitude, p.Longitude);
            return new RecommendationItemDto(
                p.PlaceId,
                overlay?.OwnerDisplayName ?? p.Name,
                p.Address,
                p.Latitude,
                p.Longitude,
                Math.Round(distance, 1),
                0,
                CardKind.Organic,
                p.OpenNow,
                overlay?.NereyeRatingAvg,
                overlay?.RatingCount ?? 0,
                p.PhotoReference,
                PlaceMedia.Url(overlay?.Photos.FirstOrDefault()?.Url, p.PhotoReference),
                overlay?.Tags.Select(t => t.Tag).ToList() ?? [],
                [],
                PlaceBlurb.Compose(p, distance, overlay?.Tags.Select(t => t.Tag).ToList()),
                p.GoogleRating,
                p.GoogleUserRatingCount,
                p.PriceLevel,
                p.Category,
                open.ClosesAt,
                open.ClosesInMinutes,
                open.OpensAt,
                open.OpensInMinutes);
        }).ToList();
        return await _covers.FillAsync(items, cancellationToken);
    }
}
