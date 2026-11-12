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

    public Task<IReadOnlyList<PlaceSnapshot>> EnrichAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default) =>
        RunAsync(places, lite: false, cancellationToken);

    public Task<IReadOnlyList<PlaceSnapshot>> EnrichLiteAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default) =>
        RunAsync(places.Take(PhotoBatch.MaxPlaces).ToList(), lite: true, cancellationToken);

    public Task<PlaceSnapshot> EnrichOneAsync(PlaceSnapshot place, CancellationToken cancellationToken = default) =>
        LookupAsync(place, lite: false, cancellationToken);

    private async Task<IReadOnlyList<PlaceSnapshot>> RunAsync(
        IReadOnlyList<PlaceSnapshot> places,
        bool lite,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = places.Select(async p =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await LookupAsync(p, lite, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    private async Task<PlaceSnapshot> LookupAsync(PlaceSnapshot place, bool lite, CancellationToken cancellationToken)
    {
        var tagged = PlacePhoto.FirstTrusted([place.PhotoReference]);
        if (tagged is not null)
        {
            return place with { PhotoReference = tagged };
        }

        var key = "places:photo:v3:" + place.PlaceId;
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return cached.Length == 0 ? place : place with { PhotoReference = cached };
        }

        string? wiki = null;
        string? nearby = null;
        string? site = null;
        string? google = null;
        try
        {
            wiki = await _wiki.FindWikidataAsync(place.Wikidata, cancellationToken);
            nearby = wiki is null
                ? await _wiki.FindNearbyAsync(place.Latitude, place.Longitude, place.Name, cancellationToken)
                : null;
            if (!lite)
            {
                site = wiki is null && nearby is null
                    ? await _sites.FindAsync(place.Website, cancellationToken)
                    : null;
                google = wiki is null && nearby is null && site is null
                    ? await _google.FindPhotoAsync(place.Name, place.Latitude, place.Longitude, cancellationToken)
                    : null;
            }
        }
        catch (Exception)
        {
            /* listing stays without a guessed photo */
        }

        var url = PlacePhoto.FirstTrusted([wiki, nearby, site, google]);
        await _cache.SetStringAsync(
            key,
            url ?? "",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = url is null ? MissTtl : HitTtl },
            cancellationToken);
        return url is null ? place : place with { PhotoReference = url };
    }
}
