using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Places;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Controllers;

[ApiController]
[Route("api/media")]
public sealed class MediaController : ControllerBase
{
    // Tek genişlik: her fotoğraf için Google'a bir kez sorulur ve kart da detay da aynı önbellek kaydını kullanır.
    // (Fotoğraf kotası günlüktür ve genişlik başına ayrı istek sayılır.)
    private static readonly int[] Widths = [800];
    private readonly GooglePlacesClient _google;
    private readonly IPlacesCache _cache;

    public MediaController(GooglePlacesClient google, IPlacesCache cache)
    {
        _google = google;
        _cache = cache;
    }

    /// <summary>
    /// Google fotoğraf adını, anahtar içermeyen CDN adresine yönlendirir. API anahtarı istemciye hiç gitmez
    /// ve görsel baytları bu sunucudan geçmez: istemci doğrudan CDN'den, istediği genişlikte indirir.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("osm")]
    [HttpGet("google")]
    public async Task<IActionResult> GooglePhoto([FromQuery] string? name, [FromQuery] int w = 800, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 700
            || !name.StartsWith("places/", StringComparison.Ordinal)
            || !name.Contains("/photos/", StringComparison.Ordinal)
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains("://", StringComparison.Ordinal)
            || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-')))
        {
            return BadRequest();
        }

        // Serbest genişlik önbelleği parçalar; en yakın standart boya yuvarla.
        var width = Widths.FirstOrDefault(x => x >= w, Widths[^1]);
        var key = PlacesCacheKey.PhotoUri(name, width);
        var uri = await _cache.TryGetAsync<string>(key, ct);
        if (string.IsNullOrEmpty(uri))
        {
            uri = await _google.PhotoUriAsync(name, width, ct);
            if (string.IsNullOrEmpty(uri))
            {
                // Bulunamayanı önbelleğe yazma: geçici bir hata saatlerce boş görsele dönüşmesin.
                return NotFound();
            }

            await _cache.SetAsync(key, PlacesCacheTtl.PhotoUri, uri, ct);
        }

        // Yönlendirme, sunucudaki fotoğraf adresi önbelleğinden uzun yaşamasın.
        Response.Headers.CacheControl = $"public, max-age={(int)PlacesCacheTtl.PhotoUri.TotalSeconds}";
        return Redirect(uri);
    }
}
