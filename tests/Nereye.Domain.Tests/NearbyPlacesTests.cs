using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class NearbyPlacesTests
{
    [Fact]
    public void WithinRadius_keeps_only_places_inside_the_requested_circle()
    {
        var originLat = 40.9885;
        var originLng = 29.0250;
        var inside = Snap("in", originLat + 0.01, originLng);
        var outside = Snap("out", originLat + 0.08, originLng);

        var kept = NearbyPlaces.WithinRadius([inside, outside], originLat, originLng, 5000);

        Assert.Equal(["in"], kept.Select(p => p.PlaceId).ToArray());
    }

    [Fact]
    public void PhotoBatch_Take_dedupes_and_caps_at_eight()
    {
        var ids = Enumerable.Range(1, 12).Select(i => $"osm:node:{i}").Concat(["osm:node:1", " ", ""]).ToList();
        var taken = PhotoBatch.Take(ids);
        Assert.Equal(8, taken.Count);
        Assert.Equal("osm:node:1", taken[0]);
        Assert.Equal("osm:node:8", taken[^1]);
        Assert.Equal(taken.Count, taken.Distinct().Count());
    }

    private static PlaceSnapshot Snap(string id, double lat, double lng) =>
        new(id, "Kafe", null, lat, lng, ["cafe"], true, null, null, null, null, [], "OSM");
}
