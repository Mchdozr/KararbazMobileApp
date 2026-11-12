using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Preferences;
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
                .Select(v => Enum.TryParse<VibeTag>(v, true, out var tag) ? tag : (VibeTag?)null)
                .Where(t => t is not null)
                .Select(t => t!.Value)
                .ToList();

        var result = await _service.RecommendAsync(
            new RecommendationQuery(lat, lng, intent, radiusMeters, openNow, vibeList, userId, skip, take),
            ct);
        return Ok(result);
    }
}
