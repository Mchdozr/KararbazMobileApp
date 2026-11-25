using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Claims;
using Nereye.Application.Places;
using Nereye.Application.Reviews;
using Nereye.Application.Venues;
using Nereye.Domain.Places;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/venues")]
public sealed class VenuesController : ControllerBase
{
    private readonly IVenueQueryService _venues;
    private readonly IReviewService _reviews;
    private readonly IClaimService _claims;

    public VenuesController(IVenueQueryService venues, IReviewService reviews, IClaimService claims)
    {
        _venues = venues;
        _reviews = reviews;
        _claims = claims;
    }

    [HttpGet("{placeId}")]
    [ETag]
    public async Task<ActionResult<VenueDetailDto>> Get(string placeId, CancellationToken ct)
    {
        Guid? uid = User.Identity?.IsAuthenticated == true ? User.Id() : null;
        var dto = await _venues.GetAsync(placeId, uid, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{placeId}/reviews")]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> Reviews(string placeId, CancellationToken ct)
    {
        Guid? viewer = User.Identity?.IsAuthenticated == true ? User.Id() : null;
        return Ok(await _reviews.ListAsync(placeId, viewer, ct));
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [VerifiedEmail]
    [HttpPost("{placeId}/reviews")]
    public async Task<ActionResult<ReviewDto>> Write(string placeId, WriteReviewRequest request, CancellationToken ct) =>
        Ok(await _reviews.WriteAsync(placeId, User.Id(), request, ct));

    [Authorize]
    [HttpDelete("reviews/{reviewId:guid}")]
    public async Task<IActionResult> DeleteReview(Guid reviewId, CancellationToken ct)
    {
        await _reviews.DeleteAsync(reviewId, User.Id(), ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("{placeId}/favorite")]
    public async Task<ActionResult<object>> IsFavorite(string placeId, CancellationToken ct) =>
        Ok(new { saved = await _reviews.IsFavoriteAsync(placeId, User.Id(), ct) });

    [Authorize]
    [HttpPost("{placeId}/favorite")]
    public async Task<IActionResult> Favorite(string placeId, CancellationToken ct)
    {
        await _reviews.FavoriteAsync(placeId, User.Id(), ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{placeId}/favorite")]
    public async Task<IActionResult> Unfavorite(string placeId, CancellationToken ct)
    {
        await _reviews.UnfavoriteAsync(placeId, User.Id(), ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{placeId}/reports")]
    public async Task<IActionResult> Report(string placeId, [FromBody] ReportBody body, CancellationToken ct)
    {
        await _reviews.ReportAsync(placeId, User.Id(), body.Reason ?? "", body.ReviewId, body.PhotoUrl, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("write")]
    [HttpPost("{placeId}/claims")]
    public async Task<ActionResult<ClaimDto>> Claim(string placeId, ClaimRequest request, CancellationToken ct) =>
        Ok(await _claims.SubmitAsync(placeId, User.Id(), request, ct));

    [Authorize(Roles = RoleNames.VenueOwner)]
    [VerifiedEmail]
    [HttpPost("reviews/{reviewId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid reviewId, [FromBody] ReplyBody body, CancellationToken ct)
    {
        await _reviews.ReplyAsync(reviewId, User.Id(), body.Text ?? "", ct);
        return NoContent();
    }

    /// <summary>Mekan sahibi etkinlik ekler (ör. "Akustik gece", bugün 20:30–22:30).</summary>
    [Authorize(Roles = RoleNames.VenueOwner)]
    [EnableRateLimiting("write")]
    [HttpPost("{placeId}/events")]
    public async Task<ActionResult<Nereye.Application.Events.VenueEventDto>> AddEvent(
        string placeId,
        Nereye.Application.Events.AddEventRequest body,
        [FromServices] Nereye.Application.Events.IEventService events,
        CancellationToken ct) =>
        Ok(await events.AddAsync(placeId, User.Id(), body, ct));

    [Authorize(Roles = RoleNames.VenueOwner)]
    [HttpDelete("{placeId}/events/{eventId:guid}")]
    public async Task<IActionResult> DeleteEvent(
        string placeId,
        Guid eventId,
        [FromServices] Nereye.Application.Events.IEventService events,
        CancellationToken ct)
    {
        await events.DeleteAsync(placeId, User.Id(), eventId, ct);
        return NoContent();
    }

    /// <summary>Sahiplenilmiş mekanın son 30 günü: görüntülenme, yol tarifi (analitiğe izin verenler) ve kaydetme.</summary>
    [Authorize(Roles = RoleNames.VenueOwner)]
    [HttpGet("{placeId}/stats")]
    public async Task<ActionResult<Nereye.Application.Analytics.VenueStatsDto>> Stats(
        string placeId,
        [FromServices] Nereye.Application.Analytics.IAnalyticsService analytics,
        CancellationToken ct) =>
        Ok(await analytics.OwnerStatsAsync(placeId, User.Id(), 30, ct));

    [Authorize(Roles = RoleNames.VenueOwner)]
    [HttpPut("{placeId}/owner")]
    public async Task<IActionResult> Owner(string placeId, [FromBody] OwnerUpdateBody body, CancellationToken ct)
    {
        await _venues.UpdateOwnerAsync(placeId, User.Id(), body.DisplayName, body.Description, ct);
        return NoContent();
    }

    [Authorize(Roles = RoleNames.VenueOwner)]
    [VerifiedEmail]
    [HttpPost("{placeId}/photos")]
    [RequestSizeLimit(3_000_000)]
    public async Task<ActionResult<object>> Photo(string placeId, AvatarUploadRequest body, CancellationToken ct)
    {
        if (!AvatarFile.TryDecodeBase64(body.Base64, out var bytes) || !AvatarFile.TryDetect(bytes, out _))
        {
            return BadRequest(new { error = "Fotoğraf yok veya desteklenmiyor." });
        }

        var url = await _venues.AddOwnerPhotoAsync(placeId, User.Id(), bytes, ct);
        return Ok(new { url });
    }

    public sealed record ReportBody(string? Reason, Guid? ReviewId, string? PhotoUrl = null);
    public sealed record ReplyBody(string? Text);
    public sealed record OwnerUpdateBody(string? DisplayName, string? Description);
}
