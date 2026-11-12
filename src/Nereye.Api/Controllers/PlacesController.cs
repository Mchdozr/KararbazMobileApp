using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Venues;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/places")]
[EnableRateLimiting("osm")]
public sealed class PlacesController : ControllerBase
{
    private readonly IVenueQueryService _venues;

    public PlacesController(IVenueQueryService venues) => _venues = venues;

    [HttpGet("{placeId}/photo")]
    public async Task<ActionResult<object>> Photo(string placeId, CancellationToken ct)
    {
        var url = await _venues.GetPhotoAsync(placeId, ct);
        return url is null ? NotFound() : Ok(new { photoUrl = url });
    }

    [HttpPost("photos")]
    public async Task<ActionResult<PlacePhotosResponse>> Photos([FromBody] PlacePhotosRequest? body, CancellationToken ct)
    {
        var photos = await _venues.GetPhotosAsync(body?.PlaceIds ?? [], ct);
        return Ok(new PlacePhotosResponse(photos));
    }
}
