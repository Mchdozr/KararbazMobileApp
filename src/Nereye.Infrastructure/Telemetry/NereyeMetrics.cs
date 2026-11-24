using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nereye.Infrastructure.Telemetry;

/// <summary>
/// Uygulamanın kendi ölçümleri ("Nereye" meter). OpenTelemetry yapılandırılmışsa OTLP ile dışarı aktarılır;
/// her durumda MetricsCollector yönetim paneli özeti için dinler.
/// </summary>
public static class NereyeMetrics
{
    public const string MeterName = "Nereye";
    public static readonly Meter Meter = new(MeterName, "1.0");

    private static readonly Counter<long> CacheLookups =
        Meter.CreateCounter<long>("nereye.cache.lookups", description: "Önbellek bakışları (layer, result=hit|stale|miss)");

    private static readonly Histogram<double> UpstreamDuration =
        Meter.CreateHistogram<double>("nereye.upstream.duration", unit: "ms", description: "Dış servis çağrı süresi (service, outcome)");

    public static void CacheLookup(string layer, string result) =>
        CacheLookups.Add(1, new TagList { { "layer", layer }, { "result", result } });

    public static void Upstream(string service, string outcome, double milliseconds) =>
        UpstreamDuration.Record(milliseconds, new TagList { { "service", service }, { "outcome", outcome } });
}

/// <summary>Dış servise giden her HTTP isteğinin süresini ve sonucunu ölçer (ok, limited=429, error, timeout).</summary>
public sealed class UpstreamMetricsHandler : DelegatingHandler
{
    private readonly string _service;

    public UpstreamMetricsHandler(string service) => _service = service;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var outcome = response.IsSuccessStatusCode ? "ok" : (int)response.StatusCode == 429 ? "limited" : "error";
            NereyeMetrics.Upstream(_service, outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Bizim iptalimiz (paralel yedek uç kazandı ya da istemci vazgeçti): hata sayılmaz.
            NereyeMetrics.Upstream(_service, "cancelled", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            NereyeMetrics.Upstream(_service, ex is TaskCanceledException ? "timeout" : "error", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
