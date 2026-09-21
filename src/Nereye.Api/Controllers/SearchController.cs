using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Recommendations;
using Nereye.Application.Venues;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/search")]
[EnableRateLimiting("osm")]
public sealed class SearchController : ControllerBase
{
    private readonly IVenueQueryService _venues;

    public SearchController(IVenueQueryService venues) => _venues = venues;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecommendationItemDto>>> Get(
        [FromQuery] string q,
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken ct) =>
        Ok(await _venues.SearchAsync(q, lat, lng, ct));
}
