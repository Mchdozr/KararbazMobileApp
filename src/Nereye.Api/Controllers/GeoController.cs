using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/geo")]
[EnableRateLimiting("osm")]
public sealed class GeoController : ControllerBase{
    private readonly IGeoSuggest _geo;

    public GeoController(IGeoSuggest geo) => _geo = geo;

    [AllowAnonymous]
    [HttpGet("suggest")]
    public async Task<ActionResult<IReadOnlyList<GeoHit>>> Suggest([FromQuery] string? q, CancellationToken ct)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
        {
            return Ok(Array.Empty<GeoHit>());
        }

        return Ok(await _geo.SuggestAsync(query, ct));
    }
}
