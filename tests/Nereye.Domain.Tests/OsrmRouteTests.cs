using System.Text.Json;
using Nereye.Domain.Routes;

namespace Nereye.Domain.Tests;

public sealed class OsrmRouteTests
{
    [Fact]
    public void Parses_geojson_leg()
    {
        using var doc = JsonDocument.Parse(
            """
            {"code":"Ok","routes":[{"distance":850.4,"duration":720.2,"geometry":{"coordinates":[[29.02,40.98],[29.03,40.99]]}}]}
            """);
        var route = OsrmRoute.Parse(doc.RootElement, "foot");
        Assert.NotNull(route);
        Assert.Equal("foot", route.Mode);
        Assert.Equal(850.4, route.DistanceMeters);
        Assert.Equal(720.2, route.DurationSeconds);
        Assert.Equal(2, route.Points.Count);
        Assert.Equal(40.98, route.Points[0].Latitude);
        Assert.Equal(29.02, route.Points[0].Longitude);
    }

    [Fact]
    public void Missing_route_is_null()
    {
        using var doc = JsonDocument.Parse("""{"code":"NoRoute","routes":[]}""");
        Assert.Null(OsrmRoute.Parse(doc.RootElement, "driving"));
    }
}
