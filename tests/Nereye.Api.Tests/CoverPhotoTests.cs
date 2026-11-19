using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nereye.Application.Places;
using Nereye.Application.Recommendations;
using Nereye.Domain;
using Nereye.Domain.Places;
using Nereye.Domain.Recommendation;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

/// <summary>Kart görseli: detayda bulunan kare kapağa döner; hafif arama tam aramayı engellemez.</summary>
public sealed class PhotoEnricherTests
{
    /// <summary>Wikimedia boş döner; Google metin araması aynı konumda fotoğraflı bir mekan döndürür.</summary>
    private sealed class Routes : HttpMessageHandler
    {
        public int GoogleCalls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Contains("googleapis", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref GoogleCalls);
                const string body = """
                    {"places":[{"id":"G1","displayName":{"text":"Köşe Kafe"},"location":{"latitude":41.0,"longitude":29.0},
                    "types":["cafe"],"primaryType":"cafe","photos":[{"name":"places/G1/photos/P1"}]}]}
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    private static (PlacePhotoEnricher Enricher, Routes Routes, IDistributedCache Cache) Build()
    {
        var routes = new Routes();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var google = new GooglePlacesClient(new HttpClient(routes), Options.Create(new GooglePlacesOptions { ApiKey = "test-key", UseFake = false }));
        var enricher = new PlacePhotoEnricher(new WikimediaPhotoClient(new HttpClient(routes)), new WebsitePhotoClient(new HttpClient(routes)), google, cache);
        return (enricher, routes, cache);
    }

    private static PlaceSnapshot Osm(int i) => new(
        $"osm:node/{900000 + i}", "Köşe Kafe", "Kadıköy", 41.0, 29.0, ["cafe"], null, null, null, null, null, [], "OSM");

    [Fact]
    public async Task Batch_spends_a_small_google_budget_and_a_skipped_place_is_found_later()
    {
        var (enricher, routes, cache) = Build();
        var places = Enumerable.Range(0, 5).Select(Osm).ToList();

        var batch = await enricher.EnrichLiteAsync(places);

        Assert.Equal(PlacePhotoEnricher.GoogleBudgetPerBatch, routes.GoogleCalls);
        Assert.Equal(PlacePhotoEnricher.GoogleBudgetPerBatch, batch.Count(p => p.PhotoReference == "places/G1/photos/P1"));
        var skipped = batch.Last();
        Assert.Null(skipped.PhotoReference);
        // Google denenmeden çıkan sonuç kesin "yok" diye yazılmaz.
        Assert.Null(await cache.GetStringAsync(PlacePhotoEnricher.FinalKey(skipped.PlaceId)));

        var full = await enricher.EnrichOneAsync(places.Last());
        Assert.Equal("places/G1/photos/P1", full.PhotoReference);

        // Bir sonraki toplu istekte bulunanlar önbellekten gelir, Google'a tekrar gidilmez.
        var calls = routes.GoogleCalls;
        await enricher.EnrichLiteAsync(places.Take(4).ToList());
        Assert.Equal(calls, routes.GoogleCalls);
    }
}

[Collection("api-flow")]
public sealed class CoverPhotoTests
{
    private readonly NereyeApiFactory _factory;

    public CoverPhotoTests(NereyeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Opening_a_venue_remembers_its_first_gallery_photo_as_the_card_cover()
    {
        using var client = _factory.CreateClient();
        var detail = JsonDocument.Parse(await client.GetStringAsync("/api/venues/nereye:kronotrop")).RootElement;
        var gallery = detail.GetProperty("google").GetProperty("photoUrls");
        Assert.True(gallery.GetArrayLength() > 0);

        var covers = _factory.Services.GetRequiredService<PlaceCovers>();
        Assert.Equal(gallery[0].GetString(), await covers.GetAsync("nereye:kronotrop"));

        // Görselsiz bir kart kapakla dolar; görseli olan kart değişmez.
        var bare = Card("nereye:kronotrop", null);
        var owned = Card("nereye:baska", "/media/uploads/0123456789abcdef0123456789abcdef.jpg");
        var filled = await covers.FillAsync([bare, owned]);
        Assert.Equal(gallery[0].GetString(), filled[0].PhotoUrl);
        Assert.Equal(owned.PhotoUrl, filled[1].PhotoUrl);

        // Toplu görsel ucu da kapağı döndürür.
        var res = await client.PostAsJsonAsync("/api/places/photos", new { placeIds = new[] { "nereye:kronotrop" } });
        var photos = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("photos");
        Assert.Equal(gallery[0].GetString(), photos.GetProperty("nereye:kronotrop").GetString());
    }

    private static RecommendationItemDto Card(string id, string? photo) => new(
        id, "Mekan", null, 41, 29, 100, 1, CardKind.Organic, null, null, 0, null, photo, [], [], "");
}
