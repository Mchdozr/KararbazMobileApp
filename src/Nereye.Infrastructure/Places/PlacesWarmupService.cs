using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Domain;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class WarmupPoint
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>
/// Popüler semtlerin OSM listesini süre dolmadan önceden çeken işin ayarları (<c>Places:Warmup</c>).
/// Points/Intents boşsa varsayılanlar kullanılır (yapılandırma bağlayıcısı listeleri birleştirdiği için
/// varsayılanlar ayrı tutulur).
/// </summary>
public sealed class PlacesWarmupOptions
{
    public const string Section = "Places:Warmup";

    public bool Enabled { get; set; }

    /// <summary>Turlar arası bekleme. Taze hücrelere dokunulmadığı için sık olması dış servise yük getirmez.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);

    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>İki Overpass sorgusu arası bekleme (genel sunucuların adil kullanım kuralı).</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>İstemcinin varsayılan yarıçapı (lib/filters.ts: 5000).</summary>
    public int RadiusMeters { get; set; } = 5000;

    public List<Intent> Intents { get; set; } = [];

    public List<WarmupPoint> Points { get; set; } = [];

    public IReadOnlyList<Intent> EffectiveIntents => Intents.Count > 0 ? Intents : Enum.GetValues<Intent>();

    public IReadOnlyList<WarmupPoint> EffectivePoints => Points.Count > 0 ? Points : DefaultPoints;

    /// <summary>İstanbul'da en çok açılan semtlerin merkezleri.</summary>
    public static readonly IReadOnlyList<WarmupPoint> DefaultPoints =
    [
        new() { Name = "Kadıköy", Latitude = 40.9885, Longitude = 29.0250 },
        new() { Name = "Moda", Latitude = 40.9810, Longitude = 29.0270 },
        new() { Name = "Beşiktaş", Latitude = 41.0430, Longitude = 29.0070 },
        new() { Name = "Taksim", Latitude = 41.0340, Longitude = 28.9790 },
        new() { Name = "Karaköy", Latitude = 41.0245, Longitude = 28.9770 },
        new() { Name = "Nişantaşı", Latitude = 41.0510, Longitude = 28.9940 },
        new() { Name = "Üsküdar", Latitude = 41.0260, Longitude = 29.0150 },
        new() { Name = "Bakırköy", Latitude = 40.9800, Longitude = 28.8720 },
        new() { Name = "Ataşehir", Latitude = 40.9920, Longitude = 29.1240 },
        new() { Name = "Caddebostan", Latitude = 40.9650, Longitude = 29.0650 },
        new() { Name = "Sultanahmet", Latitude = 41.0055, Longitude = 28.9770 },
        new() { Name = "Ortaköy", Latitude = 41.0550, Longitude = 29.0270 }
    ];
}

/// <summary>
/// Popüler semt × niyet çiftlerinin OSM listesini (Overpass) arka planda taze tutar. Yalnızca OSM katmanını
/// ısıtır: Google kotası harcanmaz (Google birleşimi kullanıcı isteğinde ~1 sn bütçeyle yapılır).
/// Taze hücreler atlanır; sorgular arasında beklenir.
/// </summary>
public sealed class PlacesWarmupService : BackgroundService
{
    private readonly PlacesWarmupOptions _options;
    private readonly IPlacesCache _cache;
    private readonly CacheRefresher _refresher;
    private readonly IClock _clock;
    private readonly IServiceScopeFactory? _scopes;
    private readonly ILogger<PlacesWarmupService>? _log;

    public PlacesWarmupService(
        PlacesWarmupOptions options,
        IPlacesCache cache,
        CacheRefresher refresher,
        IClock clock,
        IServiceScopeFactory? scopes = null,
        ILogger<PlacesWarmupService>? log = null)
    {
        _options = options;
        _cache = cache;
        _refresher = refresher;
        _clock = clock;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_scopes is null)
        {
            return;
        }

        try
        {
            await Task.Delay(_options.StartDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await using (var scope = _scopes.CreateAsyncScope())
                {
                    var overpass = scope.ServiceProvider.GetRequiredService<OverpassPlacesClient>();
                    var warmed = await RunOnceAsync(overpass, stoppingToken);
                    _log?.LogInformation("Önbellek ısıtma turu bitti: {Count} hücre tazelendi", warmed);
                }

                await Task.Delay(_options.Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            /* kapanış */
        }
    }

    /// <summary>Bir tur: bayat ya da eksik her hücreyi çeker. Tazelenen hücre sayısını döner.</summary>
    internal async Task<int> RunOnceAsync(IPlacesClient overpass, CancellationToken cancellationToken)
    {
        var warmed = 0;
        var first = true;
        foreach (var point in _options.EffectivePoints)
        {
            foreach (var intent in _options.EffectiveIntents)
            {
                var key = CachedOsmPlacesClient.Key(point.Latitude, point.Longitude, intent, _options.RadiusMeters);
                if (!await _refresher.IsStaleAsync(key, PlacesCacheTtl.OsmNearbySoft, cancellationToken))
                {
                    continue;
                }

                if (!first && _options.Pause > TimeSpan.Zero)
                {
                    await Task.Delay(_options.Pause, cancellationToken);
                }

                first = false;
                var lat = point.Latitude;
                var lng = point.Longitude;
                if (await _refresher.RefreshAsync(
                        key,
                        PlacesCacheTtl.OsmNearby,
                        ct => CachedOsmPlacesClient.FetchAsync(overpass, lat, lng, intent, _options.RadiusMeters, ct),
                        CachedOsmPlacesClient.KeepNonEmpty,
                        cancellationToken: cancellationToken))
                {
                    warmed++;
                }
                else
                {
                    _log?.LogInformation("Isıtma atlandı: {Point} / {Intent}", point.Name, intent);
                }
            }
        }

        return warmed;
    }
}
