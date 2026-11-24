using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nereye.Domain;
using Nereye.Infrastructure.Places;

namespace Nereye.Api.Tests;

/// <summary>Kendi OSRM (yaya + araç) ve Overpass sunucularımız yapılandırmayla takılır.</summary>
public sealed class ConfigurableEndpointsTests
{
    private const string OsrmBody = """
        {"code":"Ok","routes":[{"distance":600,"duration":450,"geometry":{"type":"LineString","coordinates":[[29.02,40.98],[29.03,40.99]]}}]}
        """;

    private const string OverpassBody = """
        {"elements":[{"type":"node","id":1,"lat":40.988,"lon":29.0244,"tags":{"name":"Deneme Kahve","amenity":"cafe"}}]}
        """;

    private sealed class Recorder(string body) : HttpMessageHandler
    {
        public List<Uri> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add(request.RequestUri!);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task Walking_and_driving_go_to_their_own_osrm_servers()
    {
        var handler = new Recorder(OsrmBody);
        var client = new OsrmRouteClient(new HttpClient(handler), Options.Create(new RoutingOptions
        {
            FootBaseUrl = "http://osrm-foot:5000/",
            CarBaseUrl = "http://osrm-car:5000"
        }));

        var walk = await client.FindAsync(40.98, 29.02, 40.99, 29.03, "walking");
        await client.FindAsync(40.98, 29.02, 40.99, 29.03, "driving");

        Assert.NotNull(walk);
        Assert.Equal("osrm-foot", handler.Calls[0].Host);
        Assert.StartsWith("/route/v1/walking/", handler.Calls[0].AbsolutePath);
        Assert.Equal("osrm-car", handler.Calls[1].Host);
        Assert.StartsWith("/route/v1/driving/", handler.Calls[1].AbsolutePath);
        // Gerçek yaya profili: 600 m / 450 sn gerçekçi, düzeltme uygulanmaz.
        Assert.Equal(450, walk.DurationSeconds);
    }

    [Fact]
    public async Task Routing_defaults_to_the_public_osrm_server()
    {
        var handler = new Recorder(OsrmBody);
        var client = new OsrmRouteClient(new HttpClient(handler), Options.Create(new RoutingOptions()));
        await client.FindAsync(40.98, 29.02, 40.99, 29.03, "walking");
        Assert.Equal("router.project-osrm.org", handler.Calls[0].Host);
    }

    [Fact]
    public void Invalid_urls_fall_back_to_defaults()
    {
        var options = new RoutingOptions { FootBaseUrl = "ftp://x", CarBaseUrl = "  " };
        Assert.Equal(RoutingOptions.PublicOsrm, options.FootBase.ToString());
        Assert.Equal(RoutingOptions.PublicOsrm, options.CarBase.ToString());

        var overpass = new OverpassOptions { Endpoints = ["not a url", "http://overpass/api/interpreter"] };
        Assert.Equal(["http://overpass/api/interpreter"], overpass.EffectiveEndpoints.Select(u => u.ToString()));
        Assert.Equal(3, new OverpassOptions().EffectiveEndpoints.Count);
    }

    [Fact]
    public async Task Overpass_uses_configured_endpoints_in_order()
    {
        var handler = new Recorder(OverpassBody);
        var client = new OverpassPlacesClient(
            new HttpClient(handler),
            NullLogger<OverpassPlacesClient>.Instance,
            Options.Create(new OverpassOptions { Endpoints = ["http://overpass/api/interpreter", "https://overpass-api.de/api/interpreter"] }));

        var found = await client.NearbyAsync(40.988, 29.0244, Intent.Kahve, 1000);

        Assert.Single(found);
        Assert.Equal("overpass", handler.Calls[0].Host);
        Assert.Equal("/api/interpreter", handler.Calls[0].AbsolutePath);
    }

    [Fact]
    public void Configuration_binds_routing_and_overpass_sections()
    {
        using var factory = new NereyeApiFactory();
        var routing = factory.Services.GetRequiredService<IOptions<RoutingOptions>>().Value;
        var overpass = factory.Services.GetRequiredService<IOptions<OverpassOptions>>().Value;
        Assert.Equal(RoutingOptions.PublicOsrm, routing.FootBase.ToString());
        Assert.Equal(3, overpass.EffectiveEndpoints.Count);
    }
}
