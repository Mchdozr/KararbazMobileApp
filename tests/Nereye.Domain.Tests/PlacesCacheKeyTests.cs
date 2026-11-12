using Nereye.Domain.Geo;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class PlacesCacheKeyTests
{
    [Fact]
    public void Nearby_key_is_geohash6_intent_and_radius_bucket()
    {
        var key = PlacesCacheKey.Nearby("sxk967x", Intent.Kahve, 1500);
        Assert.Equal("places:nearby:v5:sxk967:kahve:1500", key);
    }

    [Fact]
    public void Nearby_key_ignores_gps_jitter_inside_the_same_cell()
    {
        var a = PlacesCacheKey.Nearby(Geohash.Encode(40.9885, 29.0250, 6), Intent.Kahve, 5000);
        var b = PlacesCacheKey.Nearby(Geohash.Encode(40.9887, 29.0253, 6), Intent.Kahve, 5000);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Nearby_key_rounds_radius_so_close_values_share_a_bucket()
    {
        var a = PlacesCacheKey.Nearby("sxk967", Intent.Kahve, 4800);
        var b = PlacesCacheKey.Nearby("sxk967", Intent.Kahve, 5000);
        Assert.Equal(a, b);
        Assert.Equal(5000, PlacesCacheKey.BucketMeters(4800));
        Assert.Equal(8000, PlacesCacheKey.BucketMeters(5001));
    }

    [Fact]
    public void Larger_radius_bucket_can_serve_a_smaller_request()
    {
        var serving = PlacesCacheKey.ServingBuckets(5000).ToList();
        Assert.Contains(5000, serving);
        Assert.Contains(8000, serving);
        Assert.DoesNotContain(3000, serving);
        Assert.Equal(
            PlacesCacheKey.Nearby("sxk967", Intent.Kahve, 8000),
            serving.Select(b => PlacesCacheKey.Nearby("sxk967", Intent.Kahve, b)).First(k => k.EndsWith(":8000")));
    }

    [Fact]
    public void Details_key_uses_place_id()
    {
        Assert.Equal("places:details:v3:ChIJabc", PlacesCacheKey.Details("ChIJabc"));
    }
}
