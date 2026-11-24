using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Nereye.Domain;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

/// <summary>Yavaş birincil Overpass ucu ilk açılışı bekletmesin: yedek uç paralel başlar.</summary>
public sealed class OverpassHedgeTests
{
    private const string Body = """
        {"elements":[{"type":"node","id":1,"lat":40.988,"lon":29.0244,"tags":{"name":"Deneme Kahve","amenity":"cafe"}}]}
        """;

    private sealed class Handler(Func<Uri, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Hosts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Hosts)
            {
                Hosts.Add(request.RequestUri!.Host);
            }

            return respond(request.RequestUri!, cancellationToken);
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Slow_primary_is_hedged_by_the_next_endpoint()
    {
        var handler = new Handler(async (uri, ct) =>
        {
            if (uri.Host == "overpass-api.de")
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct); // birincil asılı kalıyor
            }

            return Ok();
        });
        var client = new OverpassPlacesClient(new HttpClient(handler), NullLogger<OverpassPlacesClient>.Instance)
        {
            HedgeDelay = TimeSpan.FromMilliseconds(200)
        };

        var sw = Stopwatch.StartNew();
        var found = await client.NearbyAsync(40.988, 29.0244, Intent.Kahve, 1000);
        sw.Stop();

        Assert.Single(found);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"süre {sw.Elapsed}");
        Assert.Contains("overpass.kumi.systems", handler.Hosts);
    }

    [Fact]
    public async Task Failing_primary_moves_on_without_waiting()
    {
        var handler = new Handler((uri, _) => Task.FromResult(uri.Host == "overpass-api.de" ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Ok()));
        var client = new OverpassPlacesClient(new HttpClient(handler), NullLogger<OverpassPlacesClient>.Instance)
        {
            HedgeDelay = TimeSpan.FromSeconds(30) // gecikme beklenseydi test zaman aşımına düşerdi
        };

        var sw = Stopwatch.StartNew();
        var found = await client.NearbyAsync(40.988, 29.0244, Intent.Kahve, 1000);
        Assert.Single(found);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"süre {sw.Elapsed}");
    }

    [Fact]
    public async Task All_endpoints_failing_throws()
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var client = new OverpassPlacesClient(new HttpClient(handler), NullLogger<OverpassPlacesClient>.Instance)
        {
            HedgeDelay = TimeSpan.FromMilliseconds(50)
        };

        await Assert.ThrowsAsync<HttpRequestException>(() => client.NearbyAsync(40.988, 29.0244, Intent.Kahve, 1000));
        Assert.Equal(3, handler.Hosts.Count);
    }
}
