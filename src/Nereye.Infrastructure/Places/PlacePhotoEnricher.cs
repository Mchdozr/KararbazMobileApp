using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class PlacePhotoEnricher : IPlacePhotoEnricher
{
    private static readonly TimeSpan HitTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan MissTtl = TimeSpan.FromHours(12);
    private readonly WikimediaPhotoClient _wiki;
    private readonly GooglePlacesClient _google;
    private readonly IDistributedCache _cache;

    public PlacePhotoEnricher(
        WikimediaPhotoClient wiki,
        GooglePlacesClient google,
        IDistributedCache cache)
    {
        _wiki = wiki;
        _google = google;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PlaceSnapshot>> EnrichAsync(
        IReadOnlyList<PlaceSnapshot> places,
        CancellationToken cancellationToken = default)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = places.Select(async p =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await LookupAsync(p, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    public Task<PlaceSnapshot> EnrichOneAsync(PlaceSnapshot place, CancellationToken cancellationToken = default) =>
        LookupAsync(place, cancellationToken);

    private async Task<PlaceSnapshot> LookupAsync(PlaceSnapshot place, CancellationToken cancellationToken)
    {
        var tagged = PlacePhoto.FirstTrusted([place.PhotoReference]);
        if (tagged is not null)
        {
            return place with { PhotoReference = tagged };
        }

        var key = "places:photo:v2:" + place.PlaceId;
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return cached.Length == 0 ? place : place with { PhotoReference = cached };
        }

        string? wiki = null;
        string? google = null;
        try
        {
            wiki = await _wiki.FindWikidataAsync(place.Wikidata, cancellationToken);
            google = await _google.FindPhotoAsync(place.Name, place.Latitude, place.Longitude, cancellationToken);
        }
        catch (Exception)
        {
            /* listing stays without a guessed photo */
        }

        var url = PlacePhoto.FirstTrusted([wiki, google]);
        await _cache.SetStringAsync(
            key,
            url ?? "",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = url is null ? MissTtl : HitTtl },
            cancellationToken);
        return url is null ? place : place with { PhotoReference = url };
    }
}
