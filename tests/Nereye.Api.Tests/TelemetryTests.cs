using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nereye.Application.Telemetry;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Api.Tests;

[Collection("api-flow")]
public sealed class TelemetryTests
{
    private readonly NereyeApiFactory _factory;

    public TelemetryTests(NereyeApiFactory factory) => _factory = factory;

    private static object Report(string message, string? stack = null, bool fatal = false, string screen = "/venue/[id]") => new
    {
        message,
        stack = stack ?? "at VenueScreen (index.bundle:1:100)",
        screen,
        platform = "android",
        appVersion = "1.4.0",
        fatal
    };

    [Fact]
    public async Task Client_errors_are_scrubbed_grouped_and_listed_for_admin()
    {
        using var client = _factory.CreateClient();
        var unique = "Ekran çöktü " + Guid.NewGuid().ToString("N")[..6];
        var res = await client.PostAsJsonAsync("/api/telemetry/errors", new
        {
            reports = new[]
            {
                Report(unique + " user=ali@example.com konum 40.98853,29.02504"),
                Report(unique + " user=veli@example.com konum 41.0001,28.9001"),
                Report(unique + " başka", fatal: true)
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var recent = await telemetry.RecentErrorsAsync(7);
        var mine = recent.Where(e => e.Message.StartsWith(unique, StringComparison.Ordinal)).ToList();

        Assert.Equal(2, mine.Count); // iki farklı hata: biri iki kez
        var grouped = mine.Single(e => e.Count == 2);
        Assert.DoesNotContain("@example.com", grouped.Message);
        Assert.DoesNotContain("40.98853", grouped.Message);
        Assert.Equal("android", grouped.Platform);
        Assert.Equal("1.4.0", grouped.AppVersion);
        Assert.Equal("/venue/[id]", grouped.Screen);
        Assert.True(mine.Single(e => e.Count == 1).Fatal);
    }

    [Fact]
    public async Task Oversized_or_empty_batches_are_rejected_without_storing()
    {
        using var client = _factory.CreateClient();
        var tooMany = Enumerable.Range(0, 11).Select(i => Report("x" + i)).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/telemetry/errors", new { reports = tooMany })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/telemetry/errors", new { reports = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Long_fields_are_truncated_and_unknown_platform_normalized()
    {
        using var client = _factory.CreateClient();
        var marker = "Uzun " + Guid.NewGuid().ToString("N")[..6];
        var res = await client.PostAsJsonAsync("/api/telemetry/errors", new
        {
            reports = new[]
            {
                new { message = marker + new string('m', 2000), stack = new string('s', 20_000), screen = new string('/', 500), platform = "<script>", appVersion = new string('9', 200), fatal = false }
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NereyeDbContext>();
        var row = await db.ErrorReports.SingleAsync(e => e.Message.StartsWith(marker));
        Assert.True(row.Message.Length <= 500);
        Assert.True(row.Stack!.Length <= 4000);
        Assert.True(row.Screen!.Length <= 80);
        Assert.True(row.AppVersion!.Length <= 40);
        Assert.Equal("unknown", row.Platform);
    }

    [Fact]
    public async Task Server_errors_are_recorded_too()
    {
        using var scope = _factory.Services.CreateScope();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var marker = "Sunucu " + Guid.NewGuid().ToString("N")[..6];
        await telemetry.RecordServerErrorAsync(new InvalidOperationException(marker), "/api/venues/x");
        await telemetry.RecordServerErrorAsync(new InvalidOperationException(marker), "/api/venues/x");

        var row = (await telemetry.RecentErrorsAsync(1)).Single(e => e.Message.Contains(marker));
        Assert.Equal("server", row.Source);
        Assert.Equal(2, row.Count);
        Assert.Equal("/api/venues/x", row.Screen);
    }
}
