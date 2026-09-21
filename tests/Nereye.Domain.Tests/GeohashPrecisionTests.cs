using Nereye.Domain.Geo;

namespace Nereye.Domain.Tests;

public class GeohashPrecisionTests
{
    [Fact]
    public void Recommend_precision_is_seven()
    {
        var hash = Geohash.Encode(40.9885, 29.025, Geohash.RecommendPrecision);
        Assert.Equal(7, hash.Length);
        Assert.Equal(hash, Geohash.Encode(40.9885, 29.025));
    }
}
