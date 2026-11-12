using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Recommendations;
using Nereye.Domain.Geo;
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
        [FromQuery] string? q,
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken ct)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
        {
            return Ok(Array.Empty<RecommendationItemDto>());
        }

        if (!GeoBounds.IsValid(lat, lng))
        {
            throw new ValidationException("Konum geçersiz.");
        }

        return Ok(await _venues.SearchAsync(query.Length > 80 ? query[..80] : query, lat, lng, ct));
    }
}
