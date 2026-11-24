using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Domain;
using Nereye.Domain.Geo;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

/// <summary>Elle ilerletilen saat: yumuşak süre dolmuş gibi davranmak için.</summary>
public sealed class ManualClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Kaç dış çağrı yapıldığını sayan, sonucu değiştirilebilen mekan kaynağı.</summary>
public sealed class CountingPlaces : IPlacesClient
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public Func<int, IReadOnlyList<PlaceSnapshot>> Result { get; set; } = n => [Place("osm:node:" + n, "Mekan " + n)];
    public TimeSpan Delay { get; set; }
    public bool Fail { get; set; }

    public async Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude, double longitude, Intent intent, int radiusMeters, CancellationToken cancellationToken = default)
    {
        var n = Interlocked.Increment(ref _calls);
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Fail)
        {
            throw new HttpRequestException("Overpass unavailable");
        }

        return Result(n);
    }

    public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PlaceSnapshot?>(null);

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query, double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlaceSnapshot>>([]);

    public static PlaceSnapshot Place(string id, string name) =>
        new(id, name, "Kadıköy", 40.9885, 29.025, ["cafe"], null, null, null, null, null, [], "Harita verisi: OpenStreetMap");
}

public class StaleWhileRevalidateTests
{
    private static IPlacesCache NewCache() =>
        new DistributedPlacesCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task Osm_layer_serves_stale_entry_at_once_and_refreshes_in_background()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces();
        var osm = new CachedOsmPlacesClient(overpass, cache, refresher);

        var first = await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        Assert.Equal("Mekan 1", Assert.Single(first).Name);

        // Yumuşak süre dolmadan: önbellekten, dış çağrı yok.
        clock.Advance(TimeSpan.FromHours(20));
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        await refresher.WhenIdleAsync();
        Assert.Equal(1, overpass.Calls);

        // Yumuşak süre doldu: kullanıcı beklemeden eski listeyi alır, tazeleme arkada çalışır.
        clock.Advance(TimeSpan.FromDays(2));
        overpass.Delay = TimeSpan.FromMilliseconds(300);
        var started = DateTime.UtcNow;
        var stale = await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(200));
        Assert.Equal("Mekan 1", Assert.Single(stale).Name);

        await refresher.WhenIdleAsync();
        Assert.Equal(2, overpass.Calls);
        var fresh = await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        Assert.Equal("Mekan 2", Assert.Single(fresh).Name);
        await refresher.WhenIdleAsync();
        Assert.Equal(2, overpass.Calls);
    }

    [Fact]
    public async Task Concurrent_stale_hits_start_a_single_refresh()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces();
        var osm = new CachedOsmPlacesClient(overpass, cache, refresher);
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);

        clock.Advance(TimeSpan.FromDays(3) - TimeSpan.FromHours(1));
        overpass.Delay = TimeSpan.FromMilliseconds(200);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000)));
        await refresher.WhenIdleAsync();

        Assert.Equal(2, overpass.Calls);
    }

    [Fact]
    public async Task Failed_refresh_keeps_stale_entry_and_waits_before_retrying()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces();
        var osm = new CachedOsmPlacesClient(overpass, cache, refresher);
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);

        clock.Advance(TimeSpan.FromDays(2.5));
        overpass.Fail = true;
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        await refresher.WhenIdleAsync();
        Assert.Equal(2, overpass.Calls);

        // Hata sonrası her istekte yeniden denenmez (kısa bekleme).
        var kept = await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        await refresher.WhenIdleAsync();
        Assert.Equal("Mekan 1", Assert.Single(kept).Name);
        Assert.Equal(2, overpass.Calls);

        clock.Advance(PlacesCacheTtl.Empty);
        overpass.Fail = false;
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        await refresher.WhenIdleAsync();
        Assert.Equal(3, overpass.Calls);
    }

    [Fact]
    public async Task Empty_refresh_does_not_overwrite_a_good_list()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces();
        var osm = new CachedOsmPlacesClient(overpass, cache, refresher);
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);

        clock.Advance(TimeSpan.FromDays(2.5));
        overpass.Result = _ => [];
        await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        await refresher.WhenIdleAsync();

        var kept = await osm.NearbyAsync(40.9885, 29.025, Intent.Kahve, 5000);
        Assert.Equal("Mekan 1", Assert.Single(kept).Name);
    }

    [Fact]
    public async Task Warmup_fills_only_missing_or_stale_cells_and_never_calls_google()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces();
        var options = new PlacesWarmupOptions
        {
            Enabled = true,
            Pause = TimeSpan.Zero,
            RadiusMeters = 5000,
            Intents = [Intent.Kahve, Intent.Gece],
            Points =
            [
                new WarmupPoint { Name = "Kadıköy", Latitude = 40.9885, Longitude = 29.025 },
                new WarmupPoint { Name = "Beşiktaş", Latitude = 41.043, Longitude = 29.007 }
            ]
        };
        var warmup = new PlacesWarmupService(options, cache, refresher, clock);

        var warmed = await warmup.RunOnceAsync(overpass, CancellationToken.None);
        Assert.Equal(4, warmed);
        Assert.Equal(4, overpass.Calls);

        // Taze olanlara dokunulmaz.
        Assert.Equal(0, await warmup.RunOnceAsync(overpass, CancellationToken.None));
        Assert.Equal(4, overpass.Calls);

        // Isıtılan hücre, kullanıcının OSM katmanından aynı anahtarla okunur: Overpass'e gidilmez.
        var osm = new CachedOsmPlacesClient(overpass, cache, refresher);
        await osm.NearbyAsync(40.9886, 29.0251, Intent.Gece, 5000);
        Assert.Equal(4, overpass.Calls);

        clock.Advance(PlacesCacheTtl.OsmNearbySoft);
        Assert.Equal(4, await warmup.RunOnceAsync(overpass, CancellationToken.None));
    }

    [Fact]
    public async Task Warmup_continues_when_one_cell_fails()
    {
        var clock = new ManualClock();
        var cache = NewCache();
        var refresher = new CacheRefresher(cache, clock);
        var overpass = new CountingPlaces { Fail = true };
        var options = new PlacesWarmupOptions
        {
            Enabled = true,
            Pause = TimeSpan.Zero,
            Intents = [Intent.Kahve],
            Points = [new WarmupPoint { Name = "Kadıköy", Latitude = 40.9885, Longitude = 29.025 }]
        };
        var warmup = new PlacesWarmupService(options, cache, refresher, clock);

        Assert.Equal(0, await warmup.RunOnceAsync(overpass, CancellationToken.None));
        Assert.Null(await cache.TryGetAsync<List<PlaceSnapshot>>(
            PlacesCacheKey.OsmNearby(Geohash.Encode(40.9885, 29.025, 6), Intent.Kahve, 5000)));
    }

    [Fact]
    public void Warmup_defaults_cover_popular_istanbul_districts_and_all_intents()
    {
        var options = new PlacesWarmupOptions();
        Assert.True(options.EffectivePoints.Count >= 8);
        Assert.Equal(Enum.GetValues<Intent>().Length, options.EffectiveIntents.Count);
        Assert.All(options.EffectivePoints, p => Assert.True(GeoBounds.IsValid(p.Latitude, p.Longitude)));
    }
}

/// <summary>Öneri listesinin kendisi (OSM + Google birleşimi) de bayatken sunulup arkada tazelenir.</summary>
public sealed class StaleRecommendationsFactory : NereyeApiFactory
{
    public ManualClock Clock { get; } = new();
    public CountingFakePlaces Places { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(Clock);
        services.RemoveAll<IPlacesClient>();
        services.AddScoped<IPlacesClient>(sp =>
        {
            Places.Inner = sp.GetRequiredService<FakePlacesClient>();
            return Places;
        });
    }
}

public sealed class CountingFakePlaces : IPlacesClient
{
    private int _nearby;
    public FakePlacesClient? Inner { get; set; }
    public int NearbyCalls => Volatile.Read(ref _nearby);

    public Task<IReadOnlyList<PlaceSnapshot>> NearbyAsync(
        double latitude, double longitude, Intent intent, int radiusMeters, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _nearby);
        return Inner!.NearbyAsync(latitude, longitude, intent, radiusMeters, cancellationToken);
    }

    public Task<PlaceSnapshot?> DetailsAsync(string placeId, CancellationToken cancellationToken = default) =>
        Inner!.DetailsAsync(placeId, cancellationToken);

    public Task<IReadOnlyList<PlaceSnapshot>> TextSearchAsync(
        string query, double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Inner!.TextSearchAsync(query, latitude, longitude, cancellationToken);
}

public sealed class StaleRecommendationsTests : IClassFixture<StaleRecommendationsFactory>
{
    private readonly StaleRecommendationsFactory _factory;

    public StaleRecommendationsTests(StaleRecommendationsFactory factory) => _factory = factory;

    [Fact]
    public async Task Stale_list_is_served_and_refreshed_once_in_background()
    {
        using var client = _factory.CreateClient();
        const string url = "/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500";
        var refresher = _factory.Services.GetRequiredService<CacheRefresher>();

        await GetItemsAsync(client, url);
        var afterCold = _factory.Places.NearbyCalls;
        Assert.Equal(1, afterCold);

        _factory.Clock.Advance(TimeSpan.FromHours(1));
        await GetItemsAsync(client, url);
        await refresher.WhenIdleAsync();
        Assert.Equal(1, _factory.Places.NearbyCalls);

        _factory.Clock.Advance(PlacesCacheTtl.NearbySoft);
        var stale = await GetItemsAsync(client, url);
        Assert.True(stale >= 5);
        await refresher.WhenIdleAsync();
        Assert.Equal(2, _factory.Places.NearbyCalls);

        // Tazelenen kayıt yeni damgayla saklandı: tekrar tazeleme yok.
        await GetItemsAsync(client, url);
        await refresher.WhenIdleAsync();
        Assert.Equal(2, _factory.Places.NearbyCalls);
    }

    private static async Task<int> GetItemsAsync(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("items").GetArrayLength();
    }
}
