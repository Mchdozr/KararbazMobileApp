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
    private static readonly TimeSpan DetailsTtl = TimeSpan.FromMinutes(30);
    private readonly IPlacesClient _places;
    private readonly IPlacesCache _cache;
    private readonly IPlacePhotoEnricher _photos;
    private readonly NereyeDbContext _db;
    private readonly AvatarStorage _uploads;

    public VenueQueryService(
        IPlacesClient places,
        IPlacesCache cache,
        IPlacePhotoEnricher photos,
        NereyeDbContext db,
        AvatarStorage uploads)
    {
        _places = places;
        _cache = cache;
        _photos = photos;
        _db = db;
        _uploads = uploads;
    }

    public async Task<VenueDetailDto?> GetAsync(string placeId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var place = await _cache.GetOrSetAsync(
            PlacesCacheKey.Details(placeId),
            DetailsTtl,
            async ct =>
            {
                var found = await _places.DetailsAsync(placeId, ct);
                return found is null ? null : await _photos.EnrichOneAsync(found, ct);
            },
            cancellationToken);
        if (place is null)
        {
            return null;
        }

        var overlay = await _db.Venues
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

        var google = new GoogleVenueBlock(
            place.Name,
            place.Address,
            place.Latitude,
            place.Longitude,
            place.OpenNow,
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
            place.InternetAccess);

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
        return new VenueDetailDto(placeId, google, nereye, events, note, PlaceAmenityChips.From(place));
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
            .AsSplitQuery()
            .Include(v => v.Photos)
            .FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        var owner = overlay?.Photos.Select(p => p.Url).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (!string.IsNullOrWhiteSpace(owner))
        {
            return PlaceMedia.Url(owner, null);
        }

        var place = await _cache.GetOrSetAsync(
            PlacesCacheKey.Details(placeId),
            DetailsTtl,
            async ct =>
            {
                var found = await _places.DetailsAsync(placeId, ct);
                return found is null ? null : await _photos.EnrichOneAsync(found, ct);
            },
            cancellationToken);
        return PlaceMedia.Url(null, place?.PhotoReference);
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

        var overlays = await _db.Venues
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Where(v => snapshots.Select(s => s.PlaceId).Contains(v.GooglePlaceId))
            .ToListAsync(cancellationToken);
        var map = overlays.ToDictionary(v => v.GooglePlaceId);

        return snapshots.Select(p =>
        {
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
                p.Cuisine);
        }).ToList();
    }
}
