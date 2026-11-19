using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Mekan kapak görseli kaydı. Bir mekanın gerçek karesi bir kez bulunduğunda (detay galerisi ya da görsel araması)
/// burada tutulur; öneri, arama ve kaydedilenler listeleri kendi görseli olmayan mekan için önce buraya bakar.
/// Böylece detayda görünen fotoğraf kartta stok görsel yerine çıkar ve istemci ikinci bir istek atmaz.
/// </summary>
public sealed class PlaceCovers
{
    private readonly IPlacesCache _cache;

    public PlaceCovers(IPlacesCache cache) => _cache = cache;

    public static bool IsGoogle(string url) => url.Contains("/api/media/google", StringComparison.Ordinal);

    public async Task<string?> GetAsync(string placeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = await _cache.TryGetAsync<string>(PlacesCacheKey.Cover(placeId), cancellationToken);
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IEnumerable<string> placeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = placeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        var found = await Task.WhenAll(ids.Select(async id => (id, url: await GetAsync(id, cancellationToken))));
        return found.Where(x => x.url is not null).ToDictionary(x => x.id, x => x.url!, StringComparer.Ordinal);
    }

    /// <summary>Gerçek bir kare bulunduğunda çağrılır. Boş adres kaydedilmez (bulunamadı bilgisi burada tutulmaz).</summary>
    public async Task RememberAsync(string placeId, string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            var ttl = IsGoogle(url) ? PlacesCacheTtl.CoverGoogle : PlacesCacheTtl.Cover;
            await _cache.SetAsync(PlacesCacheKey.Cover(placeId), ttl, url, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            /* kapak yalnızca hız içindir; yazılamazsa akış sürer */
        }
    }

    /// <summary>Listedeki görselsiz kartlara kayıtlı kapakları ekler. Sıra ve diğer alanlar değişmez.</summary>
    public async Task<List<Application.Recommendations.RecommendationItemDto>> FillAsync(
        IReadOnlyList<Application.Recommendations.RecommendationItemDto> items,
        CancellationToken cancellationToken = default)
    {
        var need = items.Where(i => string.IsNullOrWhiteSpace(i.PhotoUrl)).Select(i => i.PlaceId).ToList();
        if (need.Count == 0)
        {
            return items.ToList();
        }

        var covers = await GetManyAsync(need, cancellationToken);
        return items
            .Select(i => string.IsNullOrWhiteSpace(i.PhotoUrl) && covers.TryGetValue(i.PlaceId, out var url) ? i with { PhotoUrl = url } : i)
            .ToList();
    }
}
