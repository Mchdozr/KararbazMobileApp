using Microsoft.EntityFrameworkCore;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Application.Recommendations;
using Nereye.Application.Reviews;
using Nereye.Domain.Entities;
using Nereye.Domain.Places;
using Nereye.Domain.Recommendation;
using Nereye.Domain.Reviews;
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
        var authors = userIds.Count == 0
            ? []
            : await _db.Users.Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
                .ToListAsync(cancellationToken);
        var byId = authors.ToDictionary(a => a.Id);

        return reviews
            .Select(r => byId.TryGetValue(r.UserId, out var a) ? ToDto(r, a.DisplayName, a.AvatarUrl) : ToDto(r))
            .ToList();
    }

    public async Task<ReviewDto> WriteAsync(string placeId, Guid userId, WriteReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (!ReviewRules.StarsValid(request.Stars))
        {
            throw new ValidationException("Puan 1-5 olmalı.");
        }

        var tags = (request.Tags ?? []).Distinct().Take(ReviewRules.MaxTags).ToList();
        var photos = (request.PhotoUrls ?? []).Distinct().ToList();
        if (photos.Count > ReviewRules.MaxPhotos)
        {
            throw new ValidationException($"En fazla {ReviewRules.MaxPhotos} fotoğraf eklenebilir.");
        }

        if (!photos.All(ReviewRules.IsOwnUpload))
        {
            throw new ValidationException("Fotoğraf uygulamadan yüklenmeli.");
        }

        var venue = await EnsureVenueAsync(placeId, cancellationToken);
        var review = await _db.Reviews
            .Include(r => r.Tags)
            .Include(r => r.Photos)
            .Include(r => r.Reply)
            .FirstOrDefaultAsync(r => r.VenueId == venue.Id && r.UserId == userId, cancellationToken);
        if (review is null)
        {
            review = new Review
            {
                Id = Guid.NewGuid(),
                VenueId = venue.Id,
                UserId = userId,
                CreatedAt = _clock.UtcNow
            };
            _db.Reviews.Add(review);
        }
        else
        {
            // Etiket anahtarı (ReviewId, Tag): aynı etiketi silip yeniden eklemek yerine farkı uygula.
            foreach (var gone in review.Tags.Where(t => !tags.Contains(t.Tag)).ToList())
            {
                review.Tags.Remove(gone);
                _db.ReviewTags.Remove(gone);
            }

            _db.ReviewPhotos.RemoveRange(review.Photos);
            review.Photos.Clear();
        }

        review.Stars = request.Stars;
        review.Text = ReviewRules.CleanText(request.Text);
        foreach (var tag in tags.Where(t => review.Tags.All(x => x.Tag != t)))
        {
            review.Tags.Add(new ReviewTag { ReviewId = review.Id, Tag = tag });
        }

        foreach (var url in photos)
        {
            review.Photos.Add(new ReviewPhoto { Id = Guid.NewGuid(), ReviewId = review.Id, Url = url });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RecalcRatingAsync(venue.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var author = await _db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        return ToDto(review, author?.DisplayName, author?.AvatarUrl);
    }

    public async Task DeleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews
            .Include(r => r.Tags)
            .Include(r => r.Photos)
            .Include(r => r.Reply)
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Yorum bulunamadı.");
        if (review.UserId != userId)
        {
            throw new ForbiddenException("Yalnızca kendi yorumunu silebilirsin.");
        }

        var venueId = review.VenueId;
        if (review.Reply is not null)
        {
            _db.ReviewReplies.Remove(review.Reply);
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalcRatingAsync(venueId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MyReviewDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reviews = await _db.Reviews
            .Include(r => r.Tags)
            .Include(r => r.Photos)
            .Include(r => r.Reply)
            .Include(r => r.Venue)
            .Where(r => r.UserId == userId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = new List<MyReviewDto>(reviews.Count);
        foreach (var review in reviews)
        {
            var placeId = review.Venue?.GooglePlaceId;
            if (placeId is null)
            {
                continue;
            }

            var name = review.Venue?.OwnerDisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = (await _places.DetailsAsync(placeId, cancellationToken))?.Name ?? "Mekan";
            }

            items.Add(new MyReviewDto(
                review.Id,
                placeId,
                name,
                review.Stars,
                review.Text,
                review.Tags.Select(t => t.Tag).ToList(),
                review.Photos.Select(p => p.Url).ToList(),
                review.Reply?.Text,
                review.CreatedAt));
        }

        return items;
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
                PlaceBlurb.Compose(place, 0, venue.Tags.Select(t => t.Tag).ToList())));
        }

        return items;
    }

    public async Task ReportAsync(string placeId, Guid userId, string reason, Guid? reviewId, CancellationToken cancellationToken = default)
    {
        var clean = ReviewRules.CleanText(reason, ReviewRules.MaxReasonLength)
                    ?? throw new ValidationException("Şikayet nedeni gerekli.");
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        var venueId = venue?.Id;

        // Aynı kullanıcı aynı hedefi tekrar tekrar bildirirse kuyruğu şişirmesin.
        var already = await _db.Reports.AnyAsync(
            r => r.ReporterUserId == userId && !r.IsResolved && r.VenueId == venueId && r.ReviewId == reviewId,
            cancellationToken);
        if (already)
        {
            return;
        }

        _db.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            VenueId = venueId,
            ReviewId = reviewId,
            Reason = clean,
            CreatedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplyAsync(Guid reviewId, Guid ownerUserId, string text, CancellationToken cancellationToken = default)
    {
        var clean = ReviewRules.CleanText(text)
                    ?? throw new ValidationException("Yanıt boş olamaz.");
        text = clean;
        var review = await _db.Reviews.Include(r => r.Venue).Include(r => r.Reply)
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Yorum bulunamadı.");
        if (review.Venue?.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenException("Sadece işletme yanıtlayabilir.");
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
                    ?? throw new NotFoundException("Mekan bulunamadı.");
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

    private static ReviewDto ToDto(Review review, string? authorName = null, string? authorAvatarUrl = null) => new(
        review.Id,
        review.UserId,
        authorName,
        review.Stars,
        review.Text,
        review.Tags.Select(t => t.Tag).ToList(),
        review.Photos.Select(p => p.Url).ToList(),
        review.Reply?.Text,
        review.CreatedAt,
        authorAvatarUrl);
}
