using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/media")]
public sealed class MediaController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly GooglePlacesOptions _places;

    public MediaController(IHttpClientFactory http, IOptions<GooglePlacesOptions> places)
    {
        _http = http;
        _places = places.Value;
    }

    [AllowAnonymous]
    [HttpGet("google")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GooglePhoto([FromQuery] string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !name.StartsWith("places/", StringComparison.OrdinalIgnoreCase)
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains("://", StringComparison.Ordinal))
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(_places.ApiKey))
        {
            return NotFound();
        }

        var client = _http.CreateClient();
        var url = $"https://places.googleapis.com/v1/{name}/media?maxHeightPx=800&skipHttpRedirect=false";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _places.ApiKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, contentType);
    }
}
