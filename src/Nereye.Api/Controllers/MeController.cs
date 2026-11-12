using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nereye.Application.Auth;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Application.Preferences;
using Nereye.Application.Recommendations;
using Nereye.Application.Reviews;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Places;
using Nereye.Infrastructure.Persistence;
using Nereye.Domain.Places;
using Nereye.Domain.Entities;

namespace Nereye.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly IPreferenceService _preferences;
    private readonly IReviewService _reviews;
    private readonly ISavedPlaceService _places;
    private readonly AvatarStorage _avatars;
    private readonly UserManager<AppUser> _users;
    private readonly NereyeDbContext _db;
    private readonly IAuthService _auth;

    private static readonly HashSet<string> ConsentPurposes = ["location", "kvkk", "marketing"];

    public MeController(
        IPreferenceService preferences,
        IReviewService reviews,
        ISavedPlaceService places,
        AvatarStorage avatars,
        UserManager<AppUser> users,
        NereyeDbContext db,
        IAuthService auth)
    {
        _auth = auth;
        _preferences = preferences;
        _reviews = reviews;
        _places = places;
        _avatars = avatars;
        _users = users;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Profile(CancellationToken ct) =>
        Ok(await _auth.GetProfileAsync(User.Id(), ct));

    [HttpPut]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileRequest body, CancellationToken ct) =>
        Ok(await _auth.UpdateProfileAsync(User.Id(), body, ct));

    [HttpGet("reviews")]
    public async Task<ActionResult<IReadOnlyList<MyReviewDto>>> Reviews(CancellationToken ct) =>
        Ok(await _reviews.ListMineAsync(User.Id(), ct));

    [HttpGet("preferences")]
    public async Task<ActionResult<PreferenceDto>> Get(CancellationToken ct) =>
        Ok(await _preferences.GetAsync(User.Id(), ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<PreferenceDto>> Put(PreferenceDto dto, CancellationToken ct) =>
        Ok(await _preferences.UpsertAsync(User.Id(), dto, ct));

    [HttpGet("favorites")]
    public async Task<ActionResult<IReadOnlyList<RecommendationItemDto>>> Favorites(CancellationToken ct) =>
        Ok(await _reviews.ListFavoritesAsync(User.Id(), ct));

    [HttpGet("places")]
    public async Task<ActionResult<IReadOnlyList<SavedPlaceDto>>> Places(CancellationToken ct) =>
        Ok(await _places.ListAsync(User.Id(), ct));

    [HttpPost("places")]
    public async Task<ActionResult<SavedPlaceDto>> AddPlace(UpsertSavedPlaceRequest body, CancellationToken ct)
    {
        try
        {
            return Ok(await _places.AddAsync(User.Id(), body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("places/{id:guid}")]
    public async Task<IActionResult> RemovePlace(Guid id, CancellationToken ct)
    {
        await _places.RemoveAsync(User.Id(), id, ct);
        return NoContent();
    }

    /// <summary>Her amaç için en son verilen rıza durumu.</summary>
    [HttpGet("consents")]
    public async Task<ActionResult<IReadOnlyDictionary<string, bool>>> Consents(CancellationToken ct)
    {
        var uid = User.Id();
        var logs = await _db.ConsentLogs
            .Where(c => c.UserId == uid)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Purpose, c.Granted })
            .ToListAsync(ct);
        var latest = new Dictionary<string, bool>();
        foreach (var log in logs)
        {
            latest[log.Purpose] = log.Granted;
        }

        return Ok(latest);
    }

    [HttpPost("consent")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Consent([FromBody] ConsentBody body, CancellationToken ct)
    {
        var purpose = string.IsNullOrWhiteSpace(body.Purpose) ? "location" : body.Purpose.Trim().ToLowerInvariant();
        if (!ConsentPurposes.Contains(purpose))
        {
            throw new ValidationException("Bilinmeyen rıza türü.");
        }

        _db.ConsentLogs.Add(new ConsentLog
        {
            Id = Guid.NewGuid(),
            UserId = User.Id(),
            Purpose = purpose,
            Granted = body.Granted,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("avatar")]
    [EnableRateLimiting("write")]
    [RequestSizeLimit(3_000_000)]
    public async Task<ActionResult<object>> Avatar(AvatarUploadRequest body, CancellationToken ct)
    {
        if (!AvatarFile.TryDecodeBase64(body.Base64, out var bytes) || !AvatarFile.TryDetect(bytes, out _))
        {
            return BadRequest(new { error = "Fotoğraf yok veya desteklenmiyor." });
        }

        try
        {
            var url = _avatars.Save(User.Id(), bytes);
            var user = await _users.FindByIdAsync(User.Id().ToString())
                       ?? throw new InvalidOperationException("Kullanıcı yok.");
            user.AvatarUrl = url;
            await _users.UpdateAsync(user);
            return Ok(new { avatarUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("photos")]
    [EnableRateLimiting("write")]
    [RequestSizeLimit(3_000_000)]
    public async Task<ActionResult<object>> Photo(AvatarUploadRequest body, CancellationToken ct)
    {
        if (!AvatarFile.TryDecodeBase64(body.Base64, out var bytes) || !AvatarFile.TryDetect(bytes, out _))
        {
            return BadRequest(new { error = "Fotoğraf yok veya desteklenmiyor." });
        }

        try
        {
            var url = _avatars.SaveVenue(bytes);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record ConsentBody(string? Purpose, bool Granted);
}
