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
    public void Foot_route_with_car_speed_gets_walking_duration()
    {
        // Genel OSRM sunucusu yalnızca araç profilini taşır: 567 m için 87 sn (~23 km/sa) yürüyüş olamaz.
        using var doc = JsonDocument.Parse(
            """
            {"code":"Ok","routes":[{"distance":567.3,"duration":86.6,"geometry":{"coordinates":[[29.02,40.98],[29.03,40.99]]}}]}
            """);
        var foot = OsrmRoute.Parse(doc.RootElement, "foot");
        Assert.NotNull(foot);
        Assert.InRange(foot.DurationSeconds, 567.3 / 1.4, 567.3 / 1.25); // ~7 dk
        var car = OsrmRoute.Parse(doc.RootElement, "driving");
        Assert.Equal(86.6, car!.DurationSeconds);
    }

    [Fact]
    public void Route_cache_key_snaps_origin_to_about_ten_meters()
    {
        var a = Nereye.Domain.Places.PlacesCacheKey.Route(40.987412, 29.025611, 40.988, 29.0244, "foot");
        var b = Nereye.Domain.Places.PlacesCacheKey.Route(40.987436, 29.025638, 40.988, 29.0244, "foot");
        var c = Nereye.Domain.Places.PlacesCacheKey.Route(40.987412, 29.025611, 40.988, 29.0244, "driving");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Missing_route_is_null()
    {
        using var doc = JsonDocument.Parse("""{"code":"NoRoute","routes":[]}""");
        Assert.Null(OsrmRoute.Parse(doc.RootElement, "driving"));
    }
}
