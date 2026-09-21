using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nereye.Application;
using Nereye.Application.Claims;
using Nereye.Application.Places;
using Nereye.Domain.Claims;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Claims;

public sealed class ClaimService : IClaimService
{
    private readonly NereyeDbContext _db;
    private readonly IPlacesClient _places;
    private readonly UserManager<AppUser> _users;
    private readonly Application.Common.IClock _clock;

    public ClaimService(NereyeDbContext db, IPlacesClient places, UserManager<AppUser> users, Application.Common.IClock clock)
    {
        _db = db;
        _places = places;
        _users = users;
        _clock = clock;
    }

    public async Task<ClaimDto> SubmitAsync(string placeId, Guid userId, ClaimRequest request, CancellationToken cancellationToken = default)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == placeId, cancellationToken);
        if (venue is null)
        {
            var place = await _places.DetailsAsync(placeId, cancellationToken)
                        ?? throw new InvalidOperationException("Mekan yok.");
            venue = new Venue
            {
                Id = Guid.NewGuid(),
                GooglePlaceId = placeId,
            Geohash = Domain.Geo.Geohash.Encode(place.Latitude, place.Longitude, Domain.Geo.Geohash.RecommendPrecision),
                CreatedAt = _clock.UtcNow
            };
            _db.Venues.Add(venue);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var pending = await _db.VenueClaims.AnyAsync(
            c => c.VenueId == venue.Id && c.Status == ClaimStatus.Pending,
            cancellationToken);
        if (!VenueClaimRules.CanSubmit(venue.Claimed, pending))
        {
            throw new InvalidOperationException("Bu mekan için sahiplenme açılamaz.");
        }

        var claim = new VenueClaim
        {
            Id = Guid.NewGuid(),
            VenueId = venue.Id,
            UserId = userId,
            Message = request.Message,
            Status = ClaimStatus.Pending,
            CreatedAt = _clock.UtcNow
        };
        _db.VenueClaims.Add(claim);
        await _db.SaveChangesAsync(cancellationToken);
        return new ClaimDto(claim.Id, placeId, claim.Status, claim.Message, claim.CreatedAt);
    }

    public async Task ApproveAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var claim = await _db.VenueClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
                    ?? throw new InvalidOperationException("Talep yok.");
        var venue = await _db.Venues.FirstAsync(v => v.Id == claim.VenueId, cancellationToken);
        claim.Status = ClaimStatus.Approved;
        venue.Claimed = true;
        venue.OwnerUserId = claim.UserId;
        var user = await _users.FindByIdAsync(claim.UserId.ToString());
        if (user is not null && !await _users.IsInRoleAsync(user, RoleNames.VenueOwner))
        {
            await _users.AddToRoleAsync(user, RoleNames.VenueOwner);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var claim = await _db.VenueClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
                    ?? throw new InvalidOperationException("Talep yok.");
        if (!VenueClaimRules.CanReject(claim.Status))
        {
            throw new InvalidOperationException("Bu talep reddedilemez.");
        }

        claim.Status = ClaimStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
