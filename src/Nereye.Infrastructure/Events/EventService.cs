using Microsoft.EntityFrameworkCore;
using Nereye.Application.Common;
using Nereye.Application.Events;
using Nereye.Application.Places;
using Nereye.Domain.Entities;
using Nereye.Domain.Events;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Events;

public sealed class EventService : IEventService
{
    private const int MaxItems = 12;
    private readonly NereyeDbContext _db;
    private readonly IClock _clock;
    private readonly IPlacesClient _places;
    private readonly IPlacesCache _cache;

    public EventService(NereyeDbContext db, IClock clock, IPlacesClient places, IPlacesCache cache)
    {
        _db = db;
        _clock = clock;
        _places = places;
        _cache = cache;
    }

    public async Task<TonightResponse> TonightAsync(double latitude, double longitude, int radiusMeters, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var (_, to) = EventRules.TonightWindow(now);
        var earliest = now - EventRules.MaxDuration;
        // Zaman penceresi veritabanında, mesafe ve "sürüyor mu" bellekte: bir gecede şehir genelindeki etkinlik sayısı küçük.
        var candidates = await _db.CultureEvents
            .AsNoTracking()
            .Include(e => e.Venue!).ThenInclude(v => v.Photos)
            .Where(e => e.StartsAt < to && e.StartsAt >= earliest)
            .OrderBy(e => e.StartsAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var radius = SearchRadius.Clamp(radiusMeters);
        var items = new List<TonightEventDto>();
        foreach (var e in candidates.Where(e => e.Venue is not null && EventRules.IsTonight(e.StartsAt, e.EndsAt, now)))
        {
            var venue = e.Venue!;
            var place = await DetailsAsync(venue.GooglePlaceId, cancellationToken);
            var (lat, lng) = place is not null
                ? (place.Latitude, place.Longitude)
                : venue.Geohash is { Length: > 0 } gh ? Geohash.Decode(gh) : (double.NaN, double.NaN);
            if (double.IsNaN(lat))
            {
                continue;
            }

            var distance = GeoMath.HaversineMeters(latitude, longitude, lat, lng);
            if (distance > radius)
            {
                continue;
            }

            var name = venue.OwnerDisplayName ?? place?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            items.Add(new TonightEventDto(
                e.Id,
                e.Title,
                e.StartsAt,
                e.EndsAt,
                EventRules.IsLive(e.StartsAt, e.EndsAt, now),
                venue.GooglePlaceId,
                name,
                PlaceMedia.Url(venue.Photos.FirstOrDefault()?.Url, place?.PhotoReference),
                Math.Round(distance, 1)));
        }

        // Sürenler önce, sonra başlama saatine göre.
        return new TonightResponse(items.OrderByDescending(i => i.Live).ThenBy(i => i.StartsAt).Take(MaxItems).ToList());
    }

    public async Task<VenueEventDto> AddAsync(string placeId, Guid ownerId, AddEventRequest request, CancellationToken cancellationToken = default)
    {
        var venue = await OwnedVenueAsync(placeId, ownerId, cancellationToken);
        var start = DateTime.SpecifyKind(request.StartsAt.ToUniversalTime(), DateTimeKind.Utc);
        DateTime? end = request.EndsAt is { } e ? DateTime.SpecifyKind(e.ToUniversalTime(), DateTimeKind.Utc) : null;
        if (EventRules.Validate(request.Title, start, end, _clock.UtcNow) is { } error)
        {
            throw new ValidationException(error);
        }

        var entity = new CultureEvent { Id = Guid.NewGuid(), VenueId = venue.Id, Title = request.Title!.Trim(), StartsAt = start, EndsAt = end };
        _db.CultureEvents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return new VenueEventDto(entity.Id, entity.Title, entity.StartsAt, entity.EndsAt);
    }

    public async Task DeleteAsync(string placeId, Guid ownerId, Guid eventId, CancellationToken cancellationToken = default)
    {
        var venue = await OwnedVenueAsync(placeId, ownerId, cancellationToken);
        var entity = await _db.CultureEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.VenueId == venue.Id, cancellationToken)
                     ?? throw new NotFoundException("Etkinlik bulunamadı.");
        _db.CultureEvents.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Venue> OwnedVenueAsync(string placeId, Guid ownerId, CancellationToken cancellationToken)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null || venue.OwnerUserId != ownerId)
        {
            throw new ForbiddenException("Bu mekana yalnızca sahibi etkinlik ekleyebilir.");
        }

        return venue;
    }

    // Önce kart önbelleği (liste çekildiyse dolu), yoksa sağlayıcı (OSM detayı 7 gün önbellekli).
    private async Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken) =>
        await _cache.TryGetAsync<PlaceSnapshot>(PlacesCacheKey.Details(placeId), cancellationToken)
        ?? await _places.DetailsAsync(placeId, cancellationToken);
}
