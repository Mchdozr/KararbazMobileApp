using Microsoft.EntityFrameworkCore;
using Nereye.Application.Places;
using Nereye.Application.Recommendations;
using Nereye.Application.Reviews;
using Nereye.Domain.Entities;
using Nereye.Domain.Places;
using Nereye.Domain.Recommendation;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Reviews;

public sealed class ReviewService : IReviewService
{
    private readonly NereyeDbContext _db;
    private readonly IPlacesClient _places;
    private readonly IPlacePhotoEnricher _photos;
    private readonly Application.Common.IClock _clock;

    public ReviewService(NereyeDbContext db, IPlacesClient places, IPlacePhotoEnricher photos, Application.Common.IClock clock)
    {
        _db = db;
        _places = places;
        _photos = photos;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ReviewDto>> ListAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null)
        {
            return [];
        }

        var reviews = await _db.Reviews
            .Include(r => r.Tags)
            .Include(r => r.Photos)
            .Include(r => r.Reply)
            .Where(r => r.VenueId == venue.Id && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var names = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return reviews.Select(r => ToDto(r, names.GetValueOrDefault(r.UserId))).ToList();
    }

    public async Task<ReviewDto> WriteAsync(string placeId, Guid userId, WriteReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Stars is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Puan 1-5 olmalı.");
        }

        var venue = await EnsureVenueAsync(placeId, cancellationToken);
        var review = new Review
        {
            Id = Guid.NewGuid(),
            VenueId = venue.Id,
            UserId = userId,
            Stars = request.Stars,
            Text = request.Text,
            CreatedAt = _clock.UtcNow,
            Tags = request.Tags.Select(t => new ReviewTag { Tag = t }).ToList(),
            Photos = request.PhotoUrls.Select(u => new ReviewPhoto { Id = Guid.NewGuid(), Url = u }).ToList()
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalcRatingAsync(venue.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var author = await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken);
        return ToDto(review, author);
    }

    public async Task<bool> IsFavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null)
        {
            return false;
        }

        return await _db.Favorites.AnyAsync(f => f.UserId == userId && f.VenueId == venue.Id, cancellationToken);
    }

    public async Task FavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default)
    {
        var venue = await EnsureVenueAsync(placeId, cancellationToken);
        var exists = await _db.Favorites.AnyAsync(f => f.UserId == userId && f.VenueId == venue.Id, cancellationToken);
        if (!exists)
        {
            _db.Favorites.Add(new Favorite { UserId = userId, VenueId = venue.Id, CreatedAt = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnfavoriteAsync(string placeId, Guid userId, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null)
        {
            return;
        }

        var fav = await _db.Favorites.FindAsync([userId, venue.Id], cancellationToken);
        if (fav is not null)
        {
            _db.Favorites.Remove(fav);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RecommendationItemDto>> ListFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var favs = await _db.Favorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
        var venueIds = favs.Select(f => f.VenueId).ToList();
        var venues = await _db.Venues
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Where(v => venueIds.Contains(v.Id))
            .ToListAsync(cancellationToken);
        var byId = venues.ToDictionary(v => v.Id);

        var pending = new List<(Venue Venue, PlaceSnapshot Place)>();
        foreach (var fav in favs)
        {
            if (!byId.TryGetValue(fav.VenueId, out var venue))
            {
                continue;
            }

            var place = await _places.DetailsAsync(venue.GooglePlaceId, cancellationToken);
            if (place is not null)
            {
                pending.Add((venue, place));
            }
        }

        var enriched = await _photos.EnrichAsync(pending.Select(p => p.Place).ToList(), cancellationToken);
        var items = new List<RecommendationItemDto>();
        for (var i = 0; i < pending.Count; i++)
        {
            var venue = pending[i].Venue;
            var place = enriched[i];
            items.Add(new RecommendationItemDto(
                place.PlaceId,
                venue.OwnerDisplayName ?? place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                0,
                0,
                CardKind.Organic,
                place.OpenNow,
                venue.NereyeRatingAvg,
                venue.RatingCount,
                place.PhotoReference,
                PlaceMedia.Url(venue.Photos.FirstOrDefault()?.Url, place.PhotoReference),
                venue.Tags.Select(t => t.Tag).ToList(),
                [],
                PlaceBlurb.Compose(place, 0, venue.Tags.Select(t => t.Tag).ToList()),
                place.Cuisine));
        }

        return items;
    }

    public async Task ReportAsync(string placeId, Guid userId, string reason, Guid? reviewId, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        _db.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            VenueId = venue?.Id,
            ReviewId = reviewId,
            Reason = reason,
            CreatedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplyAsync(Guid reviewId, Guid ownerUserId, string text, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.Include(r => r.Venue)
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new InvalidOperationException("Yorum yok.");
        if (review.Venue?.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException("Sadece işletme yanıtlayabilir.");
        }

        if (review.Reply is not null)
        {
            review.Reply.Text = text;
        }
        else
        {
            _db.ReviewReplies.Add(new ReviewReply
            {
                Id = Guid.NewGuid(),
                ReviewId = reviewId,
                OwnerUserId = ownerUserId,
                Text = text,
                CreatedAt = _clock.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Venue> EnsureVenueAsync(string placeId, CancellationToken cancellationToken)
    {
        var existing = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var place = await _places.DetailsAsync(placeId, cancellationToken)
                    ?? throw new InvalidOperationException("Mekan bulunamadı.");
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            GooglePlaceId = placeId,
            PrimaryIntent = InferIntent(place),
            Geohash = Domain.Geo.Geohash.Encode(place.Latitude, place.Longitude, Domain.Geo.Geohash.RecommendPrecision),
            CreatedAt = _clock.UtcNow
        };
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync(cancellationToken);
        return venue;
    }

    private async Task RecalcRatingAsync(Guid venueId, CancellationToken cancellationToken)
    {
        var venue = await _db.Venues.FirstAsync(v => v.Id == venueId, cancellationToken);
        var visible = await _db.Reviews.Where(r => r.VenueId == venueId && !r.IsHidden).ToListAsync(cancellationToken);
        venue.RatingCount = visible.Count;
        venue.NereyeRatingAvg = visible.Count == 0 ? null : visible.Average(r => r.Stars);
    }

    private static Intent InferIntent(PlaceSnapshot place)
    {
        foreach (var intent in Enum.GetValues<Intent>())
        {
            if (place.Types.Any(t => IntentPlaceTypes.GoogleTypes(intent).Contains(t)))
            {
                return intent;
            }
        }

        return Intent.Yemek;
    }

    private static ReviewDto ToDto(Review review, string? authorName = null) => new(
        review.Id,
        review.UserId,
        authorName,
        review.Stars,
        review.Text,
        review.Tags.Select(t => t.Tag).ToList(),
        review.Photos.Select(p => p.Url).ToList(),
        review.Reply?.Text,
        review.CreatedAt);
}
