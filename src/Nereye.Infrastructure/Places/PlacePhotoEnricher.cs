using Microsoft.Extensions.Caching.Distributed;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class PlacePhotoEnricher : IPlacePhotoEnricher
{
    private static readonly TimeSpan HitTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan MissTtl = TimeSpan.FromHours(12);
    private readonly WikimediaPhotoClient _wiki;
    private readonly WebsitePhotoClient _sites;
    private readonly GooglePlacesClient _google;
    private readonly IDistributedCache _cache;

    public PlacePhotoEnricher(
        WikimediaPhotoClient wiki,
        WebsitePhotoClient sites,
        GooglePlacesClient google,
        IDistributedCache cache)
    {
        _wiki = wiki;
        _sites = sites;
        _google = google;
        _cache = cache;
    }

    /// <summary>
    /// Toplu kart görseli isteğinde Google'da eşleşme aranacak en fazla mekan sayısı. Google kotası kıt kaynaktır;
    /// geri kalanlar Wikimedia ve mekanın web sitesiyle denenir, bir sonraki istekte sıra onlara gelir.
    /// </summary>
    public const int GoogleBudgetPerBatch = 4;

    private static readonly TimeSpan PartialMissTtl = TimeSpan.FromHours(6);

    public Task<IReadOnlyList<PlaceSnapshot>> EnrichAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default) =>
        RunAsync(places, googleBudget: int.MaxValue, cancellationToken);

    public Task<IReadOnlyList<PlaceSnapshot>> EnrichLiteAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default) =>
        RunAsync(places.Take(PhotoBatch.MaxPlaces).ToList(), GoogleBudgetPerBatch, cancellationToken);

    public Task<PlaceSnapshot> EnrichOneAsync(PlaceSnapshot place, CancellationToken cancellationToken = default) =>
        LookupAsync(place, allowGoogle: true, cancellationToken);

    private async Task<IReadOnlyList<PlaceSnapshot>> RunAsync(
        IReadOnlyList<PlaceSnapshot> places,
        int googleBudget,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(4);
        // Sıradaki ilk kartlar (ekranda görünenler) Google bütçesini kullanır.
        var tasks = places.Select(async (p, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await LookupAsync(p, index < googleBudget, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    public static string FinalKey(string placeId) => "places:photo:v4:" + placeId;

    public static string PartialKey(string placeId) => "places:photo:v4:partial:" + placeId;

    /// <summary>
    /// Sırayla Wikidata, yakındaki Wikimedia görseli, web sitesi kapak görseli, Google eşleşmesi.
    /// "Bulunamadı" sonucu yalnızca tüm kaynaklar (Google dahil) denendiyse kesin kaydedilir; Google denenmeden
    /// çıkan sonuç ayrı (kısa ömürlü) anahtardadır ve sonraki tam aramayı engellemez.
    /// </summary>
    private async Task<PlaceSnapshot> LookupAsync(PlaceSnapshot place, bool allowGoogle, CancellationToken cancellationToken)
    {
        var tagged = PlacePhoto.FirstTrusted([place.PhotoReference]);
        if (tagged is not null)
        {
            return place with { PhotoReference = tagged };
        }

        var cached = await _cache.GetStringAsync(FinalKey(place.PlaceId), cancellationToken);
        if (cached is not null)
        {
            return cached.Length == 0 ? place : place with { PhotoReference = cached };
        }

        var freeSourcesTried = await _cache.GetStringAsync(PartialKey(place.PlaceId), cancellationToken) is not null;
        if (freeSourcesTried && !allowGoogle)
        {
            return place;
        }

        string? url = null;
        try
        {
            if (!freeSourcesTried)
            {
                var wiki = await _wiki.FindWikidataAsync(place.Wikidata, cancellationToken);
                var nearby = wiki is null
                    ? await _wiki.FindNearbyAsync(place.Latitude, place.Longitude, place.Name, cancellationToken)
                    : null;
                var site = wiki is null && nearby is null
                    ? await _sites.FindAsync(place.Website, cancellationToken)
                    : null;
                url = PlacePhoto.FirstTrusted([wiki, nearby, site]);
            }

            if (url is null && allowGoogle)
            {
                url = PlacePhoto.FirstTrusted([await _google.FindPhotoAsync(place.Name, place.Latitude, place.Longitude, cancellationToken)]);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            /* kart tahmini görselsiz kalır */
        }

        if (url is not null)
        {
            await SetAsync(FinalKey(place.PlaceId), url, HitTtl, cancellationToken);
            return place with { PhotoReference = url };
        }

        if (allowGoogle && _google.CanEnrich)
        {
            await SetAsync(FinalKey(place.PlaceId), "", MissTtl, cancellationToken);
        }
        else if (!freeSourcesTried)
        {
            await SetAsync(PartialKey(place.PlaceId), "", PartialMissTtl, cancellationToken);
        }

        return place;
    }

    private Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken) =>
        _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
}
