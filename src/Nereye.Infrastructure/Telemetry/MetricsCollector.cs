using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nereye.Application.Telemetry;
using Nereye.Domain.Telemetry;

namespace Nereye.Infrastructure.Telemetry;

/// <summary>
/// Süreç içi ölçüm özeti: ASP.NET Core'un http.server.request.duration ölçümü ile Nereye ölçümlerini dinler,
/// son pencereyi (varsayılan 15 dk) tutar ve yüzdelik hesaplar. Dışa aktarım (OTLP) bundan bağımsızdır.
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    private const int MaxSamplesPerKey = 4000;
    private readonly MeterListener _listener = new();
    private readonly TimeSpan _window;
    private readonly Func<DateTime> _now;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Sample>> _routes = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Sample>> _upstreams = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Sample>> _caches = new();

    private readonly record struct Sample(DateTime At, double Value, string Outcome);

    public MetricsCollector() : this(TimeSpan.FromMinutes(15), () => DateTime.UtcNow)
    {
    }

    public MetricsCollector(TimeSpan window, Func<DateTime> now)
    {
        _window = window;
        _now = now;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == NereyeMetrics.MeterName
                || (instrument.Meter.Name == "Microsoft.AspNetCore.Hosting" && instrument.Name == "http.server.request.duration"))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>(OnDouble);
        _listener.SetMeasurementEventCallback<long>(OnLong);
        _listener.Start();
    }

    public MetricsSnapshot Snapshot()
    {
        var cutoff = _now() - _window;
        var routes = _routes
            .Select(kv => (kv.Key, Samples: Recent(kv.Value, cutoff)))
            .Where(x => x.Samples.Count > 0)
            .Select(x =>
            {
                var ms = x.Samples.Select(s => s.Value).ToArray();
                return new RouteStat(x.Key, ms.Length, x.Samples.Count(s => s.Outcome == "5xx"),
                    Round(Percentiles.Of(ms, 0.5)), Round(Percentiles.Of(ms, 0.95)), Round(Percentiles.Of(ms, 0.99)));
            })
            .OrderByDescending(r => r.Count)
            .ToList();
        var upstreams = _upstreams
            .Select(kv => (kv.Key, Samples: Recent(kv.Value, cutoff)))
            .Where(x => x.Samples.Count > 0)
            .Select(x =>
            {
                var done = x.Samples.Where(s => s.Outcome != "cancelled").ToList();
                var ms = done.Select(s => s.Value).ToArray();
                return new UpstreamStat(x.Key, done.Count,
                    done.Count(s => s.Outcome is "error" or "timeout"),
                    done.Count(s => s.Outcome == "limited"),
                    Round(Percentiles.Of(ms, 0.5)), Round(Percentiles.Of(ms, 0.95)));
            })
            .OrderBy(u => u.Service)
            .ToList();
        var caches = _caches
            .Select(kv => (kv.Key, Samples: Recent(kv.Value, cutoff)))
            .Where(x => x.Samples.Count > 0)
            .Select(x =>
            {
                long hits = x.Samples.Count(s => s.Outcome == "hit"), stale = x.Samples.Count(s => s.Outcome == "stale");
                long misses = x.Samples.Count(s => s.Outcome == "miss");
                long total = x.Samples.Count;
                return new CacheStat(x.Key, total, hits, stale, misses, Percentiles.Ratio(hits + stale, total));
            })
            .OrderBy(c => c.Layer)
            .ToList();
        return new MetricsSnapshot(_now(), Environment.MachineName, (int)_window.TotalMinutes, routes, upstreams, caches);
    }

    public void Dispose() => _listener.Dispose();

    private void OnDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name == "http.server.request.duration")
        {
            string? route = null, method = null;
            var status = 0;
            foreach (var tag in tags)
            {
                switch (tag.Key)
                {
                    case "http.route": route = tag.Value as string; break;
                    case "http.request.method": method = tag.Value as string; break;
                    case "http.response.status_code": status = Convert.ToInt32(tag.Value ?? 0); break;
                }
            }

            // Rotası olmayan istekler (statik dosya, 404) tek satırda toplanır: kart görselleri listeyi doldurmasın.
            var key = route is null ? "(statik ve diğer)" : $"{method} {route}";
            Add(_routes, key, value * 1000, status >= 500 ? "5xx" : "ok");
            return;
        }

        if (instrument.Name == "nereye.upstream.duration")
        {
            Add(_upstreams, Tag(tags, "service") ?? "?", value, Tag(tags, "outcome") ?? "ok");
        }
    }

    private void OnLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name == "nereye.cache.lookups")
        {
            Add(_caches, Tag(tags, "layer") ?? "?", value, Tag(tags, "result") ?? "miss");
        }
    }

    private void Add(ConcurrentDictionary<string, ConcurrentQueue<Sample>> map, string key, double value, string outcome)
    {
        var queue = map.GetOrAdd(key, _ => new ConcurrentQueue<Sample>());
        queue.Enqueue(new Sample(_now(), value, outcome));
        while (queue.Count > MaxSamplesPerKey && queue.TryDequeue(out _))
        {
        }
    }

    private static List<Sample> Recent(ConcurrentQueue<Sample> queue, DateTime cutoff)
    {
        while (queue.TryPeek(out var head) && head.At < cutoff && queue.TryDequeue(out _))
        {
        }

        return queue.Where(s => s.At >= cutoff).ToList();
    }

    private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
            {
                return tag.Value as string;
            }
        }

        return null;
    }

    private static double? Round(double? v) => v is { } x ? Math.Round(x, 1) : null;
}

/// <summary>Özeti 30 sn'de bir paylaşılan önbelleğe yazar; yönetim paneli buradan okur.</summary>
public sealed class MetricsPublisher : BackgroundService
{
    internal const string Key = "metrics:api:latest";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MetricsCollector _collector;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MetricsPublisher>? _log;

    public MetricsPublisher(MetricsCollector collector, IDistributedCache cache, ILogger<MetricsPublisher>? log = null)
    {
        _collector = collector;
        _cache = cache;
        _log = log;
    }

    public async Task PublishOnceAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_collector.Snapshot(), Json);
        await _cache.SetStringAsync(Key, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                try
                {
                    await PublishOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log?.LogWarning(ex, "Ölçüm özeti yazılamadı");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

public sealed class DistributedMetricsReader : IMetricsReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;

    public DistributedMetricsReader(IDistributedCache cache) => _cache = cache;

    public async Task<MetricsSnapshot?> LatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _cache.GetStringAsync(MetricsPublisher.Key, cancellationToken);
            return raw is null ? null : JsonSerializer.Deserialize<MetricsSnapshot>(raw, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
