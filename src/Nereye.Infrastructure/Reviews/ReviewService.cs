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
    private readonly IPlacesCache _cache;
    private readonly Places.AvatarStorage _uploads;
    private readonly Places.PlaceCovers _covers;

    public ReviewService(
        NereyeDbContext db,
        IPlacesClient places,
        IPlacePhotoEnricher photos,
        Application.Common.IClock clock,
        IPlacesCache cache,
        Places.AvatarStorage uploads,
        Places.PlaceCovers covers)
    {
        _covers = covers;
        _uploads = uploads;
        _cache = cache;
        _db = db;
        _places = places;
        _photos = photos;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ReviewDto>> ListAsync(string placeId, Guid? viewerId = null, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null)
        {
            return [];
        }

        var reviews = await _db.Reviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Tags)
            .Include(r => r.Photos)
            .Include(r => r.Reply)
            .Where(r => r.VenueId == venue.Id && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        if (viewerId is { } viewer)
        {
            var blocked = await _db.UserBlocks
                .Where(b => b.BlockerUserId == viewer)
                .Select(b => b.BlockedUserId)
                .ToListAsync(cancellationToken);
            if (blocked.Count > 0)
            {
                reviews = reviews.Where(r => !blocked.Contains(r.UserId)).ToList();
            }
        }

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
        List<string> dropped = [];
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

            // Fotoğraflarda da fark uygulanır: kalanlar yerinde durur, çıkanların dosyası kayıttan sonra silinir.
            foreach (var gone in review.Photos.Where(p => !photos.Contains(p.Url)).ToList())
            {
                dropped.Add(gone.Url);
                review.Photos.Remove(gone);
                _db.ReviewPhotos.Remove(gone);
            }
        }

        review.Stars = request.Stars;
        review.Text = ReviewRules.CleanText(request.Text);
        foreach (var tag in tags.Where(t => review.Tags.All(x => x.Tag != t)))
        {
            review.Tags.Add(new ReviewTag { ReviewId = review.Id, Tag = tag });
        }

        foreach (var url in photos.Where(u => review.Photos.All(p => p.Url != u)))
        {
            // Açıkça Added: anahtarı dolu yeni çocuk, izlenen bir yoruma eklenince EF onu UPDATE sanmasın.
            var photo = new ReviewPhoto { Id = Guid.NewGuid(), ReviewId = review.Id, Url = url };
            _db.ReviewPhotos.Add(photo);
            review.Photos.Add(photo);
        }

        await _db.SaveChangesAsync(cancellationToken);
        foreach (var url in dropped)
        {
            _uploads.DeleteUpload(url);
        }

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

        var files = review.Photos.Select(p => p.Url).ToList();
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(cancellationToken);
        // Yorum gidince fotoğraf dosyaları da gider: bağlantıyı bilen biri silinmiş kareyi açamasın.
        foreach (var url in files)
        {
            _uploads.DeleteUpload(url);
        }

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
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
        var venueIds = favs.Select(f => f.VenueId).ToList();
        var venues = await _db.Venues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Tags)
            .Include(v => v.Photos)
            .Where(v => venueIds.Contains(v.Id))
            .ToListAsync(cancellationToken);
        var byId = venues.ToDictionary(v => v.Id);

        // Favori başına sırayla ve önbelleksiz dış çağrı yerine: önbellekten, hepsi aynı anda.
        var ordered = favs.Where(f => byId.ContainsKey(f.VenueId)).Select(f => byId[f.VenueId]).ToList();
        var places = await Task.WhenAll(ordered.Select(v => _cache.GetOrSetAsync(
            PlacesCacheKey.Details(v.GooglePlaceId),
            PlacesCacheTtl.Nearby,
            ct => _places.DetailsAsync(v.GooglePlaceId, ct),
            cancellationToken)));
        var pending = new List<(Venue Venue, PlaceSnapshot Place)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (places[i] is { } place)
            {
                pending.Add((ordered[i], place));
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
                place.GoogleRating,
                place.GoogleUserRatingCount,
                place.PriceLevel,
                place.Category));
        }

        return await _covers.FillAsync(items, cancellationToken);
    }

    public async Task ReportAsync(
        string placeId,
        Guid userId,
        string reason,
        Guid? reviewId,
        string? photoUrl = null,
        CancellationToken cancellationToken = default)
    {
        var clean = ReviewRules.CleanText(reason, ReviewRules.MaxReasonLength)
                    ?? throw new ValidationException("Şikayet nedeni gerekli.");
        var photo = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
        if (photo is not null && !ReviewRules.IsOwnUpload(photo))
        {
            throw new ValidationException("Bildirilen fotoğraf bulunamadı.");
        }

        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        var venueId = venue?.Id;

        // Aynı kullanıcı aynı hedefi tekrar tekrar bildirirse kuyruğu şişirmesin.
        var already = await _db.Reports.AnyAsync(
            r => r.ReporterUserId == userId && !r.IsResolved && r.VenueId == venueId && r.ReviewId == reviewId && r.PhotoUrl == photo,
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
            PhotoUrl = photo,
            Reason = clean,
            CreatedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task BlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        if (blockerId == blockedId)
        {
            throw new ValidationException("Kendini engelleyemezsin.");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == blockedId, cancellationToken))
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        if (!await _db.UserBlocks.AnyAsync(b => b.BlockerUserId == blockerId && b.BlockedUserId == blockedId, cancellationToken))
        {
            _db.UserBlocks.Add(new UserBlock { BlockerUserId = blockerId, BlockedUserId = blockedId, CreatedAt = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnblockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockerUserId == blockerId && b.BlockedUserId == blockedId,
            cancellationToken);
        if (row is not null)
        {
            _db.UserBlocks.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<BlockedUserDto>> BlockedAsync(Guid blockerId, CancellationToken cancellationToken = default) =>
        await _db.UserBlocks
            .Where(b => b.BlockerUserId == blockerId)
            .OrderByDescending(b => b.CreatedAt)
            .Join(_db.Users, b => b.BlockedUserId, u => u.Id, (b, u) => new BlockedUserDto(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);

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

    private static Intent InferIntent(PlaceSnapshot place) =>
        IntentPlaceTypes.IntentOf(place.Types, place.Name, place.Cuisine) ?? Intent.Yemek;

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
