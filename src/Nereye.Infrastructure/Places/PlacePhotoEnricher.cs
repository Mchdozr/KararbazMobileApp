using Microsoft.Extensions.Caching.Distributed;
using Nereye.Application.Places;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class PlacePhotoEnricher : IPlacePhotoEnricher
{
    private static readonly TimeSpan HitTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan MissTtl = TimeSpan.FromHours(12);
    private readonly WikimediaPhotoClient _wiki;
    private readonly WebsitePhotoClient _web;
    private readonly IDistributedCache _cache;

    public PlacePhotoEnricher(WikimediaPhotoClient wiki, WebsitePhotoClient web, IDistributedCache cache)
    {
        _wiki = wiki;
        _web = web;
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
                return await LookupAsync(p, includeWebsite: false, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    public Task<PlaceSnapshot> EnrichOneAsync(PlaceSnapshot place, CancellationToken cancellationToken = default) =>
        LookupAsync(place, includeWebsite: true, cancellationToken);

    private async Task<PlaceSnapshot> LookupAsync(
        PlaceSnapshot place,
        bool includeWebsite,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(place.PhotoReference))
        {
            return place;
        }

        var key = "places:photo:" + place.PlaceId;
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return string.IsNullOrEmpty(cached) ? place : place with { PhotoReference = cached };
        }

        string? url = null;
        try
        {
            url = await _wiki.FindWikidataAsync(place.Wikidata, cancellationToken)
                  ?? (includeWebsite ? await _web.FindAsync(place.Website, cancellationToken) : null)
                  ?? await _wiki.FindNearbyAsync(place.Latitude, place.Longitude, place.Name, cancellationToken);
        }
        catch (Exception)
        {
            /* keep listing without photo */
        }

        await _cache.SetStringAsync(
            key,
            url ?? "",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = url is null ? MissTtl : HitTtl },
            cancellationToken);
        return url is null ? place : place with { PhotoReference = url };
    }
}
