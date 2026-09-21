using Nereye.Domain;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class PlacesCacheKeyTests
{
    [Fact]
    public void Nearby_key_includes_geohash_intent_and_radius()
    {
        var key = PlacesCacheKey.Nearby("sxk967", Intent.Kahve, 1500, 40.988, 29.025);
        Assert.Equal("places:nearby:sxk967:kahve:1500:40.988:29.025", key);
    }

    [Fact]
    public void Details_key_uses_place_id()
    {
        Assert.Equal("places:details:ChIJabc", PlacesCacheKey.Details("ChIJabc"));
    }
}
