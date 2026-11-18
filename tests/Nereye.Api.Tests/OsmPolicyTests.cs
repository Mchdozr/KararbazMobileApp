using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Domain;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

/// <summary>Public OSM servislerinin kullanım kurallarını ve Google zengin alan sınırını sabitleyen testler.</summary>
public sealed class OsmPolicyTests
{
    private const string SuggestJson = """
        [{"place_id":1,"lat":"40.99","lon":"29.03","name":"Moda","display_name":"Moda, Kadıköy, İstanbul",
          "class":"place","type":"suburb","addresstype":"suburb","address":{"suburb":"Moda","city_district":"Kadıköy","city":"İstanbul"}}]
        """;

    [Fact]
    public async Task Suggest_does_not_ask_nominatim_twice_for_the_same_query()
    {
        var handler = new CountingHandler(SuggestJson);
        var client = Nominatim(handler, new NominatimGate(TimeSpan.FromMilliseconds(10)));

        var first = await client.SuggestAsync("Moda");
        var second = await client.SuggestAsync("  moda ");

        Assert.NotEmpty(first);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task At_most_one_nominatim_request_per_second_for_the_whole_process()
    {
        var handler = new CountingHandler(SuggestJson);
        var gate = new NominatimGate(); // gerçek 1 sn aralık
        // Farklı istemci örnekleri aynı kapıyı paylaşır (DI'da tekil).
        var a = Nominatim(handler, gate);
        var b = Nominatim(handler, gate);

        await Task.WhenAll(a.SuggestAsync("Moda"), b.SuggestAsync("Fenerbahçe"), a.SuggestAsync("Yeldeğirmeni"));

        var starts = handler.Starts.OrderBy(t => t).ToList();
        Assert.Equal(3, starts.Count);
        for (var i = 1; i < starts.Count; i++)
        {
            Assert.True(starts[i] - starts[i - 1] >= TimeSpan.FromMilliseconds(990), $"aralık {(starts[i] - starts[i - 1]).TotalMilliseconds} ms");
        }
    }

    [Fact]
    public async Task Autocomplete_gives_up_instead_of_queueing_for_long()
    {
        var gate = new NominatimGate(TimeSpan.FromSeconds(5));
        await gate.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None);
        await Assert.ThrowsAsync<NominatimBusyException>(() =>
            gate.RunAsync(_ => Task.FromResult(2), TimeSpan.FromMilliseconds(100), CancellationToken.None));
    }

    [Fact]
    public async Task Overpass_results_are_cached_and_failures_are_not()
    {
        var inner = new CountingPlaces();
        var cache = new DistributedPlacesCache(Memory());
        var client = new CachedOsmPlacesClient(inner, cache);

        await client.NearbyAsync(40.9885, 29.025, Intent.Kahve, 1500);
        await client.NearbyAsync(40.98851, 29.02501, Intent.Kahve, 1500);
        Assert.Equal(1, inner.NearbyCalls);

        inner.Fail = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => client.NearbyAsync(41.05, 29.0, Intent.Yemek, 1500));
        inner.Fail = false;
        await client.NearbyAsync(41.05, 29.0, Intent.Yemek, 1500);
        Assert.Equal(3, inner.NearbyCalls);
    }

    [Fact]
    public async Task Card_level_details_never_trigger_the_rich_google_lookup()
    {
        var osm = new CountingPlaces();
        var google = new RichCountingGoogle();
        var client = new GoogleAugmentedPlacesClient(osm, google, new GooglePlacesOptions { ApiKey = "k" }, new DistributedPlacesCache(Memory()));

        await client.DetailsAsync("osm:node:1");
        await client.DetailsAsync("g:abc");
        Assert.Equal(0, google.RichCalls);
        Assert.Equal(0, google.FindRichCalls);

        await client.RichDetailsAsync("g:abc");
        Assert.Equal(1, google.RichCalls);
        await client.RichDetailsAsync("osm:node:1");
        Assert.Equal(1, google.FindRichCalls);
    }

    [Fact]
    public void List_mask_has_no_atmosphere_fields_and_the_detail_mask_does()
    {
        foreach (var field in new[] { "editorialSummary", "reviews", "serves", "outdoorSeating", "goodFor", "allowsDogs" })
        {
            Assert.DoesNotContain(field, GooglePlaceJson.ListMask);
            Assert.DoesNotContain(field, GooglePlaceJson.DetailLeanMask);
        }

        Assert.Contains("editorialSummary", GooglePlaceJson.DetailMask);
        Assert.Contains("reviews.authorAttribution", GooglePlaceJson.SearchRichMask);
    }

    [Fact]
    public void Photo_redirect_is_not_cached_for_a_day()
    {
        Assert.True(PlacesCacheTtl.PhotoUri <= TimeSpan.FromHours(1));
        Assert.True(PlacesCacheTtl.OsmNearby > PlacesCacheTtl.Nearby);
    }

    private static NominatimPlacesClient Nominatim(HttpMessageHandler handler, NominatimGate gate) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") }, gate, new DistributedPlacesCache(Memory()));

    private static IDistributedCache Memory() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private sealed class CountingHandler(string body) : HttpMessageHandler
    {
        public ConcurrentBag<DateTimeOffset> Starts { get; } = [];

        public int Calls => Starts.Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Starts.Add(DateTimeOffset.UtcNow);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingPlaces : IPlacesClient
    {
        public int NearbyCalls;
        public bool Fail;

        public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(double latitude, double longitude, Intent intent, int radiusMeters, CancellationToken cancellationToken = default)
        {
            NearbyCalls++;
            if (Fail)
            {
                throw new HttpRequestException("overpass kapalı");
            }

            return Task.FromResult<IReadOnlyList<PlaceSnapshot>>([Place("osm:node:1", "Petra")]);
        }

        public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaceSnapshot?>(Place(placeId, "Petra"));

        public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(string query, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);
    }

    private sealed class RichCountingGoogle : IPlacesClient, IRichPlaceDetails, IRichPlaceSource
    {
        public int RichCalls;
        public int FindRichCalls;

        public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(double latitude, double longitude, Intent intent, int radiusMeters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);

        public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaceSnapshot?>(Place(placeId, "Petra"));

        public Task<PlaceSnapshot?> RichDetailsAsync(string placeId, CancellationToken cancellationToken = default)
        {
            RichCalls++;
            return Task.FromResult<PlaceSnapshot?>(Place(placeId, "Petra"));
        }

        public Task<PlaceSnapshot?> FindRichAsync(PlaceSnapshot target, CancellationToken cancellationToken = default)
        {
            FindRichCalls++;
            return Task.FromResult<PlaceSnapshot?>(null);
        }

        public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(string query, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);
    }

    private static PlaceSnapshot Place(string id, string name) =>
        new(id, name, "Kadıköy", 40.9885, 29.025, ["cafe"], true, null, null, null, null, [], "Harita verisi: OpenStreetMap");
}
