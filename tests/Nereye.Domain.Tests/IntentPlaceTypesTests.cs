using Nereye.Domain;
using Nereye.Domain.Places;

namespace Nereye.Domain.Tests;

public class IntentPlaceTypesTests
{
    [Fact]
    public void Kahve_includes_cafe()
    {
        Assert.Contains("cafe", IntentPlaceTypes.GoogleTypes(Intent.Kahve));
        Assert.Contains("coffee_shop", IntentPlaceTypes.GoogleTypes(Intent.Kahve));
    }

    [Fact]
    public void Kultur_includes_theater_and_museum()
    {
        var types = IntentPlaceTypes.GoogleTypes(Intent.Kultur);
        Assert.Contains("performing_arts_theater", types);
        Assert.Contains("museum", types);
        Assert.Contains("movie_theater", types);
    }

    [Fact]
    public void Every_intent_has_at_least_one_type()
    {
        foreach (var intent in Enum.GetValues<Intent>())
        {
            Assert.NotEmpty(IntentPlaceTypes.GoogleTypes(intent));
            Assert.NotEmpty(IntentPlaceTypes.OsmTags(intent));
            Assert.False(string.IsNullOrWhiteSpace(IntentPlaceTypes.NominatimQuery(intent)));
        }
    }

    [Fact]
    public void Osm_cafe_maps_to_google_cafe()
    {
        Assert.Equal("cafe", IntentPlaceTypes.GoogleTypeFromOsm("cafe", null, null, null));
        Assert.Equal("park", IntentPlaceTypes.GoogleTypeFromOsm(null, null, "park", null));
    }
}
