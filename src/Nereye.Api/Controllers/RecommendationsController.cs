using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Preferences;
using Nereye.Domain.Geo;
using Nereye.Application.Recommendations;
using Nereye.Domain.Places;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[EnableRateLimiting("osm")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;
    private readonly IPreferenceService _prefs;

    public RecommendationsController(IRecommendationService service, IPreferenceService prefs)
    {
        _service = service;
        _prefs = prefs;
    }

    [HttpGet]
    public async Task<ActionResult<RecommendationResponse>> Get(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] Intent intent = Intent.Kahve,
        [FromQuery] int radiusMeters = 1500,
        [FromQuery] bool openNow = false,
        [FromQuery] string? vibes = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        if (!GeoBounds.IsValid(lat, lng))
        {
            throw new ValidationException("Konum geçersiz.");
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 50);
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.Id() : null;
        if (userId is { } uid && radiusMeters == 1500)
        {
            var pref = await _prefs.GetAsync(uid, ct);
            if (pref.DefaultRadiusMeters > 0)
            {
                radiusMeters = pref.DefaultRadiusMeters;
            }
        }

        radiusMeters = SearchRadius.Clamp(radiusMeters);

        var vibeList = string.IsNullOrWhiteSpace(vibes)
            ? []
            : vibes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => Enum.TryParse<VibeTag>(v, ignoreCase: true, out var tag) && Enum.IsDefined(tag) ? tag : (VibeTag?)null)
                .Where(v => v is not null)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

        var result = await _service.RecommendAsync(
            new RecommendationQuery(lat, lng, intent, radiusMeters, openNow, vibeList, userId, skip, take),
            ct);
        return Ok(result);
    }
}
