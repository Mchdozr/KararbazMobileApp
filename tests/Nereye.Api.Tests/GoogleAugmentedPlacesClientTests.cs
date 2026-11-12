using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Domain;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

public class GoogleAugmentedPlacesClientTests
{
    [Fact]
    public async Task Nearby_returns_osm_without_waiting_out_the_google_budget()
    {
        var osm = new StubPlaces(Osm("osm:node:1", "Petra"), TimeSpan.FromMilliseconds(20));
        var google = new StubPlaces(Osm("g1", "Other", 40.99, 29.04), TimeSpan.FromMilliseconds(400));
        var cache = new DistributedPlacesCache(Memory());
        var client = new GoogleAugmentedPlacesClient(osm, google, Keyed(), cache) { GoogleBudget = TimeSpan.FromMilliseconds(80) };

        var started = DateTime.UtcNow;
        var result = await client.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        var elapsed = DateTime.UtcNow - started;

        Assert.Equal(["osm:node:1"], result.Select(p => p.PlaceId).ToArray());
        Assert.True(elapsed < TimeSpan.FromMilliseconds(250), elapsed.ToString());
    }

    [Fact]
    public async Task Nearby_merges_google_when_it_arrives_inside_the_budget()
    {
        var osm = new StubPlaces(Osm("osm:node:1", "Petra Roasting"), TimeSpan.FromMilliseconds(10));
        var google = new StubPlaces(Osm("gid", "Petra Roasting", 40.98851, 29.02501) with
        {
            Phone = "0216",
            GoogleRating = 4.6
        }, TimeSpan.FromMilliseconds(10));
        var client = new GoogleAugmentedPlacesClient(osm, google, Keyed(), new DistributedPlacesCache(Memory()))
        {
            GoogleBudget = TimeSpan.FromMilliseconds(200)
        };

        var result = await client.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);

        var petra = Assert.Single(result);
        Assert.Equal("osm:node:1", petra.PlaceId);
        Assert.Equal("0216", petra.Phone);
        Assert.Equal(4.6, petra.GoogleRating);
    }

    [Fact]
    public async Task Nearby_writes_late_google_merge_into_the_places_cache()
    {
        var osm = new StubPlaces(Osm("osm:node:1", "Petra Roasting"), TimeSpan.Zero);
        var google = new StubPlaces(Osm("gid", "Petra Roasting", 40.98851, 29.02501) with { Phone = "0216" }, TimeSpan.FromMilliseconds(120));
        var cache = new DistributedPlacesCache(Memory());
        var client = new GoogleAugmentedPlacesClient(osm, google, Keyed(), cache) { GoogleBudget = TimeSpan.FromMilliseconds(40) };

        var first = await client.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        Assert.Null(Assert.Single(first).Phone);

        await Task.Delay(250);
        var key = PlacesCacheKey.Nearby(Nereye.Domain.Geo.Geohash.Encode(40.9885, 29.025, 6), Intent.Kahve, 5000);
        var cached = await cache.TryGetAsync<List<PlaceSnapshot>>(key);
        Assert.NotNull(cached);
        Assert.Equal("0216", Assert.Single(cached).Phone);
    }

    private static GooglePlacesOptions Keyed() => new() { ApiKey = "test-key" };

    private static IDistributedCache Memory() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static PlaceSnapshot Osm(string id, string name, double lat = 40.9885, double lng = 29.025) =>
        new(id, name, "Kadıköy", lat, lng, ["cafe"], true, null, null, null, null, [], "Harita verisi: OpenStreetMap");

    private sealed class StubPlaces : IPlacesClient
    {
        private readonly IReadOnlyList<PlaceSnapshot> _result;
        private readonly TimeSpan _delay;

        public StubPlaces(PlaceSnapshot result, TimeSpan delay)
        {
            _result = [result];
            _delay = delay;
        }

        public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
            double latitude, double longitude, Intent intent, int radiusMeters, CancellationToken cancellationToken = default)
        {
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return _result;
        }

        public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaceSnapshot?>(null);

        public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
            string query, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);
    }
}
