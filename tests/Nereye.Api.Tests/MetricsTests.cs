using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nereye.Application.Telemetry;
using Nereye.Infrastructure.Telemetry;

namespace Nereye.Api.Tests;

public sealed class MetricsCollectorTests
{
    [Fact]
    public void Collector_summarizes_upstream_and_cache_measurements()
    {
        using var collector = new MetricsCollector();
        var service = "test-" + Guid.NewGuid().ToString("N")[..6];
        for (var i = 1; i <= 20; i++)
        {
            NereyeMetrics.Upstream(service, i <= 18 ? "ok" : "error", i * 10);
        }

        NereyeMetrics.CacheLookup("unit-" + service, "hit");
        NereyeMetrics.CacheLookup("unit-" + service, "hit");
        NereyeMetrics.CacheLookup("unit-" + service, "stale");
        NereyeMetrics.CacheLookup("unit-" + service, "miss");

        var snap = collector.Snapshot();
        var up = Assert.Single(snap.Upstreams, u => u.Service == service);
        Assert.Equal(20, up.Count);
        Assert.Equal(2, up.Errors);
        Assert.Equal(190, up.P95Ms);
        var cache = Assert.Single(snap.Caches, c => c.Layer == "unit-" + service);
        Assert.Equal(4, cache.Lookups);
        Assert.Equal(0.75, cache.HitRatio); // bayat da sunulan kayıttır: isabet sayılır
    }

    [Fact]
    public void Window_drops_old_samples()
    {
        var now = DateTime.UtcNow;
        using var collector = new MetricsCollector(TimeSpan.FromMinutes(15), () => now);
        var service = "old-" + Guid.NewGuid().ToString("N")[..6];
        NereyeMetrics.Upstream(service, "ok", 100);
        now = now.AddMinutes(16);
        Assert.DoesNotContain(collector.Snapshot().Upstreams, u => u.Service == service);
    }
}

[Collection("api-flow")]
public sealed class MetricsEndpointTests
{
    private readonly NereyeApiFactory _factory;

    public MetricsEndpointTests(NereyeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_sees_route_latency_and_cache_hit_ratio()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500")).EnsureSuccessStatusCode();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/metrics")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@nereye.app", password = "Admin!12345" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var snap = await client.GetFromJsonAsync<MetricsSnapshot>("/api/admin/metrics", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(snap);
        var route = Assert.Single(snap.Routes, r => r.Route == "GET api/recommendations");
        Assert.True(route.Count >= 3);
        Assert.NotNull(route.P95Ms);
        var nearby = Assert.Single(snap.Caches, c => c.Layer == "nearby");
        Assert.True(nearby.Lookups >= 3);
        Assert.True(nearby.HitRatio > 0);
    }

    [Fact]
    public async Task Snapshot_is_published_for_the_admin_panel()
    {
        var publisher = _factory.Services.GetRequiredService<MetricsPublisher>();
        await publisher.PublishOnceAsync(CancellationToken.None);
        var reader = _factory.Services.GetRequiredService<IMetricsReader>();
        var snap = await reader.LatestAsync();
        Assert.NotNull(snap);
        Assert.True(snap.WindowMinutes > 0);
    }
}

/// <summary>OTLP ucu verilince dışa aktarım açılır; toplayıcı yokken bile uygulama sorunsuz başlar ve yanıt verir.</summary>
public sealed class OtlpApiFactory : NereyeApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("OpenTelemetry:Endpoint", "http://127.0.0.1:4317");
    }
}

public sealed class OtlpExportTests : IClassFixture<OtlpApiFactory>
{
    private readonly OtlpApiFactory _factory;

    public OtlpExportTests(OtlpApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Api_runs_with_otlp_export_enabled()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/recommendations?lat=40.9885&lng=29.0250&intent=Kahve&radiusMeters=1500")).EnsureSuccessStatusCode();
        Assert.NotNull(_factory.Services.GetService<OpenTelemetry.Metrics.MeterProvider>());
    }
}
