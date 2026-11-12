using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Routes;
using Nereye.Domain.Geo;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/routes")]
[EnableRateLimiting("osm")]
public sealed class RoutesController : ControllerBase
{
    private readonly IRouteService _routes;

    public RoutesController(IRouteService routes) => _routes = routes;

    [HttpGet]
    public async Task<ActionResult<RouteDto>> Get(
        [FromQuery] double fromLat,
        [FromQuery] double fromLng,
        [FromQuery] double toLat,
        [FromQuery] double toLng,
        [FromQuery] string mode = "foot",
        CancellationToken ct = default)
    {
        if (!GeoBounds.IsValid(fromLat, fromLng) || !GeoBounds.IsValid(toLat, toLng))
        {
            throw new ValidationException("Konum geçersiz.");
        }

        if (mode is not ("foot" or "driving"))
        {
            mode = "foot";
        }

        var route = await _routes.GetAsync(fromLat, fromLng, toLat, toLng, mode, ct);
        return route is null ? NotFound() : Ok(route);
    }
}
