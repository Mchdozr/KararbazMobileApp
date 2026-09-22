using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Routes;

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
        var route = await _routes.GetAsync(fromLat, fromLng, toLat, toLng, mode, ct);
        return route is null ? NotFound() : Ok(route);
    }
}
