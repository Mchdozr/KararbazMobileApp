using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Events;
using Nereye.Domain.Geo;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/events")]
[EnableRateLimiting("osm")]
public sealed class EventsController : ControllerBase
{
    private readonly IEventService _events;

    public EventsController(IEventService events) => _events = events;

    /// <summary>Yakındaki bu akşamın etkinlikleri (İstanbul saatiyle sabah 04:00'e kadar); sürenler önce.</summary>
    [HttpGet("tonight")]
    public async Task<ActionResult<TonightResponse>> Tonight(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int radiusMeters = 5000,
        CancellationToken ct = default)
    {
        if (!GeoBounds.IsValid(lat, lng))
        {
            throw new ValidationException("Konum geçersiz.");
        }

        return Ok(await _events.TonightAsync(lat, lng, radiusMeters, ct));
    }
}
